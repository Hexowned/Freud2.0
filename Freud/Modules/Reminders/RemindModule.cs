#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Freud.Common.Attributes;
using Freud.Common.Configuration;
using Freud.Common.Tasks;
using Freud.Database.Db;
using Freud.Database.Db.Entities;
using Freud.Exceptions;
using Freud.Extensions;
using Freud.Extensions.Discord;
using Humanizer;
using Humanizer.Localisation;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Reminders
{
    [Group("remind"), Module(ModuleType.Reminders), NotBlocked]
    [Description("Manage reminders.")]
    [Aliases("reminders", "reminder", "todo", "todolist", "note")]
    [UsageExampleArgs("1h Finish homework assignment")]
    [Cooldown(3, 5, CooldownBucketType.Channel)]
    public partial class RemindModule : FreudModule
    {
        public RemindModule(SharedData shared, DatabaseContextBuilder dcb)
            : base(shared, dcb)
        {
            this.ModuleColor = DiscordColor.LightGray;
        }

        [GroupCommand, Priority(3)]
        public Task ExecuteGroupAsync(CommandContext ctx,
                                     [Description("Time span until reminder.")] TimeSpan timespan,
                                     [Description("Channel to send message to")] DiscordChannel channel,
                                     [RemainingText, Description("What to send")] string message)
            => this.AddReminderAsync(ctx, timespan, channel, message);

        [GroupCommand, Priority(2)]
        public Task ExecuteGroupAsync(CommandContext ctx,
                                     [Description("Channel to send message to.")] DiscordChannel channel,
                                     [Description("Time span until reminder.")] TimeSpan timespan,
                                     [RemainingText, Description("What to send?")] string message)
            => this.AddReminderAsync(ctx, timespan, channel, message);

        [GroupCommand, Priority(1)]
        public Task ExecuteGroupAsync(CommandContext ctx,
                                     [Description("Time span until reminder.")] TimeSpan timespan,
                                     [RemainingText, Description("What to send?")] string message)
            => this.AddReminderAsync(ctx, timespan, null, message);

        [GroupCommand, Priority(0)]
        public Task ExecuteGroupAsync(CommandContext ctx,
                                     [Description("Channel for which to list the reminders.")] DiscordChannel channel = null)
            => channel is null ? this.ListAsync(ctx) : this.ListAsync(ctx, channel);

        #region HELPER_FUNCTIONS

        //
        private async Task AddReminderAsync(CommandContext ctx, TimeSpan timespan, DiscordChannel channel, string message, bool repeat = false)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new InvalidCommandUsageException("Missing time or repeat string.");

            if (message.Length > 250)
                throw new InvalidCommandUsageException("Message must be shorter than 250 characters.");

            if (timespan < TimeSpan.Zero || timespan.TotalMinutes < 1 || timespan.TotalDays > 31)
                throw new InvalidCommandUsageException("Time span cannot be less than 1 minute or greater than 31 days.");

            if (!(channel is null) && !channel.PermissionsFor(ctx.Member).HasPermission(Permissions.AccessChannels | Permissions.SendMessages))
                throw new CommandFailedException("You cannot send a reminder to that channel!");

            if (channel is null & await ctx.Client.CreateDmChannelAsync(ctx.User.Id) is null)
                throw new CommandFailedException("I cannot send Direct Messages to you, please enable it so that I can remind you.");

            bool privileged;
            using (var dc = this.Database.CreateContext())
                privileged = dc.PrivilegedUsers.Any(u => u.UserId == ctx.User.Id);

            if (ctx.User.Id != ctx.Client.CurrentApplication?.Owner.Id && !privileged)
            {
                if (this.Shared.RemindExecuters.TryGetValue(ctx.User.Id, out var texecs) && texecs.Count >= 20)
                    throw new CommandFailedException("You cannot have more than 20 reminders scheduled simutaneously");
            }

            var when = DateTimeOffset.Now + timespan;

            var task = new SendMessageTaskInfo(channel?.Id ?? 0, ctx.User.Id, message, when, repeat, timespan);
            await SavedTaskExecutor.ScheduleAsync(this.Shared, this.Database, ctx.Client, task);

            if (repeat)
                await this.InformAsync(ctx, StaticDiscordEmoji.AlarmClock, $"I will repeatedly remind {channel?.Mention ?? "you"} every {Formatter.Bold(timespan.Humanize(4, minUnit: TimeUnit.Second))} to:\n\n{message}", important: false);
            else
                await this.InformAsync(ctx, StaticDiscordEmoji.AlarmClock, $"I will remind {channel?.Mention ?? "you"} in {Formatter.Bold(timespan.Humanize(4, minUnit: TimeUnit.Second))} ({when.ToUtcTimestamp()}) to:\n\n{message}", important: false);
        }

        #endregion HELPER_FUNCTIONS

        #region COMMAND_REMIND_CLEAR

        //
        [Command("deleteall"), UsageInteractivity]
        [Description("Delete all your reminders. You can also specify a channel for which to remove reminders.")]
        [Aliases("removeall", "rmrf", "rma", "clearall", "clear", "delall", "da")]
        public async Task DeleteAsync(CommandContext ctx,
                                     [Description("Channel for which to remove reminders.")] DiscordChannel channel = null)
        {
            if (!(channel is null) && channel.Type != ChannelType.Text)
                throw new InvalidCommandUsageException("You must specify a text channel for the deletion");

            if (!(await ctx.WaitForBoolReplyAsync("Are you sure you want to remove all of your reminders" + (channel is null ? "?" : $"in {channel.Mention}?"))))
                return;

            List<DatabaseReminder> reminders;
            using (var dc = this.Database.CreateContext())
            {
                if (channel is null)
                    reminders = await dc.Reminders.Where(r => r.UserId == ctx.User.Id).ToListAsync();
                else
                    reminders = await dc.Reminders.Where(r => r.UserId == ctx.User.Id && r.ChannelId == channel.Id).ToListAsync();
            }

            await Task.WhenAll(reminders.Select(r => SavedTaskExecutor.UnscheduleAsync(this.Shared, ctx.User.Id, r.Id)));
            await this.InformAsync(ctx, "Successfully removed the specified reminders.", important: false);
        }

        #endregion COMMAND_REMIND_CLEAR

        #region COMMAND_REMIND_DELETE

        //
        [Command("delete")]
        [Description("Unschedules reminders.")]
        [Aliases("-", "remove", "rm", "del", "-=", ">", ">>", "unschedule")]
        [UsageExampleArgs("1")]
        public async Task DeleteAsync(CommandContext ctx,
                                    [Description("Reminder ID.")] params int[] ids)
        {
            if (ids is null || !ids.Any())
                throw new InvalidCommandUsageException("Missing IDs of the reminders to remove");

            if (!this.Shared.RemindExecuters.TryGetValue(ctx.User.Id, out ConcurrentDictionary<int, SavedTaskExecutor> texecs))
                throw new CommandFailedException("You currently have no reminders schedule.");

            var sb = new StringBuilder();
            foreach (int id in ids)
            {
                if (!texecs.TryGetValue(id, out _))
                {
                    sb.AppendLine($"Reminder with ID {Formatter.Bold(id.ToString())} does not exist (or it is not scheuled by you)!");
                    continue;
                }

                await SavedTaskExecutor.UnscheduleAsync(this.Shared, ctx.User.Id, id);
            }

            if (sb.Length > 0)
                await this.InformOfFailureAsync(ctx, $"Action finished with the following warnings/errors:\n\n{sb.ToString()}");
            else
                await this.InformAsync(ctx, "Successfully removed all of the specified remidners.", important: false);
        }

        #endregion COMMAND_REMIND_DELETE

        #region COMMAND_REMIND_LIST

        //
        [Command("list"), Priority(1)]
        [Description("Lists your reminders.")]
        [Aliases("ls", "rlist", "remlist")]
        public Task ListAsync(CommandContext ctx,
                             [Description("Channel for which to list the reminders.")] DiscordChannel channel)
        {
            if (channel.Type != ChannelType.Text)
                throw new InvalidCommandUsageException("Reminders can only be issued for text channels.");

            if (!this.Shared.RemindExecuters.TryGetValue(ctx.User.Id, out ConcurrentDictionary<int, SavedTaskExecutor> texecs) || !texecs.Values.Any(t => (t.TaskInfo as SendMessageTaskInfo).ChannelId == channel.Id))
                throw new CommandFailedException("No reminders are scheuled for that channel.");

            return ctx.SendCollectionInPagesAsync(
                $"Reminders for channel {channel.Name}:",
                texecs.Values
                    .Select(t => (TaskId: t.Id, TaskInfo: t.TaskInfo as SendMessageTaskInfo))
                    .Where(tup => tup.TaskInfo.ChannelId == channel.Id)
                    .OrderBy(tup => tup.TaskInfo.ExecutionTime),
                tup =>
                {
                    (int id, var tinfo) = tup;
                    if (tinfo.IsRepeating)
                    {
                        return $"ID: {Formatter.Bold(id.ToString())} (repeating every {tinfo.RepeatingInterval.Humanize()}):{Formatter.BlockCode(tinfo.Message)}";
                    } else
                    {
                        if (tinfo.TimeUntilExecution > TimeSpan.FromDays(1))
                            return $"ID: {Formatter.Bold(id.ToString())} ({tinfo.ExecutionTime.ToUtcTimestamp()}):{Formatter.BlockCode(tinfo.Message)}";
                        else
                            return $"ID: {Formatter.Bold(id.ToString())} (in {tinfo.TimeUntilExecution.Humanize(precision: 3, minUnit: TimeUnit.Minute)}):{Formatter.BlockCode(tinfo.Message)}";
                    }
                },
                this.ModuleColor, 1
            );
        }

        [Command("list"), Priority(0)]
        public Task ListAsync(CommandContext ctx)
        {
            if (!this.Shared.RemindExecuters.TryGetValue(ctx.User.Id, out var texecs))
                throw new CommandFailedException("You haven't issued any reminders!");

            return ctx.SendCollectionInPagesAsync(
                "Your reminders:",
                texecs.Values
                    .Select(t => (TaskId: t.Id, TaskInfo: (SendMessageTaskInfo)t.TaskInfo))
                    .OrderBy(tup => tup.TaskInfo.ExecutionTime),
                tup =>
                {
                    (int id, SendMessageTaskInfo tinfo) = tup;
                    if (tinfo.IsRepeating)
                    {
                        return $"ID: {Formatter.Bold(id.ToString())} ({tinfo.RepeatingInterval.Humanize()}): {Formatter.BlockCode(tinfo.Message)}";
                    } else
                    {
                        if (tinfo.TimeUntilExecution > TimeSpan.FromDays(1))
                            return $"ID: {Formatter.Bold(id.ToString())} ({tinfo.ExecutionTime.ToUtcTimestamp()}): {Formatter.BlockCode(tinfo.Message)}";
                        else
                            return $"ID: {Formatter.Bold(id.ToString())} (in {tinfo.TimeUntilExecution.Humanize(precision: 3, minUnit: TimeUnit.Minute)}): {Formatter.BlockCode(tinfo.Message)}";
                    }
                },
                this.ModuleColor, 1
            );
        }

        #endregion COMMAND_REMIND_LIST

        #region COMMAND_REMIND_REPEAT

        //
        [Command("repeat"), Priority(2)]
        [Description("Schedule a new repeating reminder. You can also specify a channel where to send the reminder.")]
        [Aliases("newrep", "+r", "ar", "+=r", "<r", "<<r")]
        [UsageExampleArgs("1h Do 50 pushups!")]
        public Task RepeatAsync(CommandContext ctx,
                               [Description("Repeat timespan.")] TimeSpan timespan,
                               [Description("Channel to send message to.")] DiscordChannel channel,
                               [RemainingText, Description("What to send?")] string message)
            => this.AddReminderAsync(ctx, timespan, channel, message, true);

        [Command("repeat"), Priority(1)]
        public Task RepeatAsync(CommandContext ctx,
                            [Description("Channel to send message to.")] DiscordChannel channel,
                            [Description("Repeat timespan.")] TimeSpan timespan,
                            [RemainingText, Description("What to send?")] string message)
            => this.AddReminderAsync(ctx, timespan, channel, message, true);

        [Command("repeat"), Priority(0)]
        public Task RepeatAsync(CommandContext ctx,
                            [Description("Repeat timespan.")] TimeSpan timespan,
                            [RemainingText, Description("What to send?")] string message)
            => this.AddReminderAsync(ctx, timespan, null, message, true);

        #endregion COMMAND_REMIND_REPEAT
    }
}

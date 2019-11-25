﻿#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using Freud.Common.Attributes;
using Freud.Common.Configuration;
using Freud.Database.Db;
using Freud.Exceptions;
using Freud.Extensions.Discord;
using System;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Polls
{
    [Group("poll"), Module(ModuleType.Polls), NotBlocked, UsageInteractivity]
    [Description("Starts a new poll in the current channel. You can provide also the time for the poll to run.")]
    [UsageExampleArgs("Do you vote for User1 or User2?", "5m Do you vote for User1 or User2?")]
    [Cooldown(3, 5, CooldownBucketType.Channel)]
    public class PollModule : FreudModule
    {
        public PollModule(SharedData shared, DatabaseContextBuilder dcb)
            : base(shared, dcb)
        {
            this.ModuleColor = DiscordColor.Orange;
        }

        [GroupCommand, Priority(2)]
        public async Task ExecuteGroupAsync(CommandContext ctx,
                                           [Description("Time for poll to run.")] TimeSpan timeout,
                                           [RemainingText, Description("Question.")] string question)
        {
            if (string.IsNullOrWhiteSpace(question))
                throw new InvalidCommandUsageException("Poll requires a question.");

            if (PollService.IsPollRunningInChannel(ctx.Channel.Id))
                throw new CommandFailedException("Another poll is already running in this channel.");

            if (timeout < TimeSpan.FromSeconds(10) || timeout >= TimeSpan.FromDays(1))
                throw new InvalidCommandUsageException("Poll cannot run for less than 10 seconds or more than 1 day(s).");

            var poll = new Poll(ctx.Client.GetInteractivity(), ctx.Channel, ctx.Member, question);
            PollService.RegisterPollInChannel(poll, ctx.Channel.Id);
            try
            {
                await this.InformAsync(ctx, StaticDiscordEmoji.Question, "And what will be the possible answers? (separate with a semicolon)");
                var options = await ctx.WaitAndParsePollOptionsAsync();
                if (options is null || options.Count < 2 || options.Count > 10)
                    throw new CommandFailedException("Poll must have minimum 2 and maximum 10 options!");
                poll.Options = options;

                await poll.RunAsync(timeout);
            } finally
            {
                PollService.UnregisterPollInChannel(ctx.Channel.Id);
            }
        }

        [GroupCommand, Priority(1)]
        public Task ExecuteGroupAsync(CommandContext ctx,
                                     [Description("Question.")] string question,
                                     [Description("Time for poll to run.")] TimeSpan timeout)
            => this.ExecuteGroupAsync(ctx, timeout, question);

        [GroupCommand, Priority(0)]
        public Task ExecuteGroupAsync(CommandContext ctx, [RemainingText, Description("Question.")] string question)
            => this.ExecuteGroupAsync(ctx, TimeSpan.FromMinutes(1), question);

        #region COMMAND_STOP

        [Command("stop")]
        [Description("Stops a running poll.")]
        [Aliases("end", "cancel")]
        public Task StopAsync(CommandContext ctx)
        {
            var poll = PollService.GetPollInChannel(ctx.Channel.Id);
            if (poll is null || poll is ReactionsPoll)
                throw new CommandFailedException("There are no text polls running in this channel.");

            if (!ctx.Member.PermissionsIn(ctx.Channel).HasPermission(Permissions.Administrator) && ctx.User.Id != poll.Initiator.Id)
                throw new CommandFailedException("You do not have the sufficient permissions to close another person's poll!");

            poll.Stop();

            return Task.CompletedTask;
        }

        #endregion COMMAND_STOP
    }
}

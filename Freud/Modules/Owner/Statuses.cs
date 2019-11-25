#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Freud.Common.Attributes;
using Freud.Database.Db;
using Freud.Database.Db.Entities;
using Freud.Exceptions;
using Freud.Extensions.Discord;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Owner
{
    public partial class OwnerModule
    {
        [Group("statuses"), NotBlocked]
        [Description("Bot status manipulation. If invoked without command, either lists or adds status depending if argument is given.")]
        [Aliases("status", "botstatus", "activity", "activities")]
        [RequireOwner]
        public class StatusModule : FreudModule
        {
            public StatusModule(SharedData shared, DatabaseContextBuilder dcb)
                : base(shared, dcb)
            {
                this.ModuleColor = DiscordColor.NotQuiteBlack;
            }

            [GroupCommand, Priority(1)]
            public Task ExecuteGroupAsync(CommandContext ctx)
                => this.ListAsync(ctx);

            [GroupCommand, Priority(0)]
            public Task ExecuteGroupAsync(CommandContext ctx,
                                         [Description("Activity  type (Playing/Watching/Streaming/ListeningTo).")] ActivityType activity,
                                         [RemainingText, Description("Status.")] string status)
                => this.SetAsync(ctx, activity, status);

            #region COMMAND_STATUS_ADD

            [Command("add")]
            [Description("Add a status to running status queue.")]
            [Aliases("+", "a", "<", "<<", "+=")]
            [UsageExampleArgs("Playing CS:GO", "Streaming on Twitch")]
            public async Task AddAsync(CommandContext ctx,
                                      [Description("Activity type (Playing/Watching/Streaming/ListeningTo).")] ActivityType activity,
                                      [RemainingText, Description("Status.")] string status)
            {
                if (string.IsNullOrWhiteSpace(status))
                    throw new InvalidCommandUsageException("Missing status.");

                if (status.Length > 60)
                    throw new CommandFailedException("Status length cannot be greater than 60 characters.");

                using (var dc = this.Database.CreateContext())
                {
                    dc.BotStatuses.Add(new DatabaseBotStatus { Activity = activity, Status = status });
                    await dc.SaveChangesAsync();
                }

                await this.InformAsync(ctx, $"Added new status: {Formatter.InlineCode(status)}", important: false);
            }

            #endregion COMMAND_STATUS_ADD

            #region COMMAND_STATUS_DELETE

            [Command("delete")]
            [Description("Remove status from running queue.")]
            [Aliases("-", "remove", "rm", "del", ">", ">>", "-=")]
            [UsageExampleArgs("2")]
            public async Task DeleteAsync(CommandContext ctx, [Description("Status ID.")] int id)
            {
                using (var dc = this.Database.CreateContext())
                {
                    dc.BotStatuses.Remove(new DatabaseBotStatus { Id = id });
                    await dc.SaveChangesAsync();
                }

                await this.InformAsync(ctx, $"Removed status with ID {Formatter.Bold(id.ToString())}", important: false);
            }

            #endregion COMMAND_STATUS_DELETE

            #region COMMAND_STATUS_LIST

            [Command("list")]
            [Description("List all bot statuses.")]
            [Aliases("ls", "l", "print")]
            public async Task ListAsync(CommandContext ctx)
            {
                List<DatabaseBotStatus> statuses;
                using (var dc = this.Database.CreateContext())
                    statuses = await dc.BotStatuses.ToListAsync();

                await ctx.SendCollectionInPagesAsync("Statuses:", statuses, status
                    => $"{Formatter.InlineCode($"{status.Id:D2}")}: {status.Activity} - {status.Status}", this.ModuleColor, 10);
            }

            #endregion COMMAND_STATUS_LIST

            #region COMMAND_STATUS_SET_ROTATION

            [Command("setrotation")]
            [Description("Set automatic rotation of bot statuses.")]
            [Aliases("sr", "setr")]
            [UsageExampleArgs("off")]
            public Task SetRoationAsync(CommandContext ctx, [Description("Enabled?")] bool enable = true)
            {
                this.Shared.StatusRotationEnabled = enable;

                return this.InformAsync(ctx, $"Status rotation {(enable ? "enabled" : "disabled")}");
            }

            #endregion COMMAND_STATUS_SET_ROTATION

            #region COMMAND_STATUS_SET_STATUS

            [Command("set"), Priority(1)]
            [Description("Set status to given string or status with given index in database. This sets rotation to false.")]
            [Aliases("s")]
            [UsageExampleArgs("Playing with fire", "5")]
            public async Task SetAsync(CommandContext ctx,
                                      [Description("Activity type (Playing/Watching/Streaming/ListeningTo).")] ActivityType type,
                                      [RemainingText, Description("Status.")] string status)
            {
                if (string.IsNullOrWhiteSpace(status))
                    throw new InvalidCommandUsageException("Missing status.");

                if (status.Length > 60)
                    throw new CommandFailedException("Status length cannot be greater than 60 characters.");

                var activity = new DiscordActivity(status, type);

                this.Shared.StatusRotationEnabled = false;
                await ctx.Client.UpdateStatusAsync(activity);
                await this.InformAsync(ctx, $"Successfully switched my current status to: {activity.ToString()}", important: false);
            }

            [Command("set"), Priority(0)]
            public async Task SetAsync(CommandContext ctx, [Description("Status ID.")] int id)
            {
                DatabaseBotStatus status;
                using (var dc = this.Database.CreateContext())
                    status = await dc.BotStatuses.FindAsync(id);

                if (status is null)
                    throw new CommandFailedException("Status with the given ID doesn't exist!, or is not in the current server!");

                var activity = new DiscordActivity(status.Status, status.Activity);

                this.Shared.StatusRotationEnabled = false;
                await ctx.Client.UpdateStatusAsync(activity);
                await this.InformAsync(ctx, $"Successfully switch my current status to: {activity.ToString()}", important: false);
            }

            #endregion COMMAND_STATUS_SET_STATUS
        }
    }
}

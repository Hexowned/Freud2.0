#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using Freud.Common.Attributes;
using Freud.Database.Db;
using Freud.Database.Db.Entities;
using Freud.Exceptions;
using Freud.Extensions.Discord;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Owner
{
    public partial class OwnerModule
    {
        [Group("blockedusers"), NotBlocked]
        [Description("Manipulate blocked users. Bot will not allow blocked users to invoke commands and will not react (either with text or emoji) to their messages.")]
        [Aliases("bu", "blockedu", "blockuser", "busers", "buser", "busr")]
        [RequirePrivilegedUser]
        public class BlockedUsersModule : FreudModule
        {
            public BlockedUsersModule(SharedData shared, DatabaseContextBuilder dcb)
                : base(shared, dcb)
            {
                this.ModuleColor = DiscordColor.NotQuiteBlack;
            }

            [GroupCommand, Priority(3)]
            public Task ExecuteGroupAsync(CommandContext ctx)
                => this.ListAsync(ctx);

            [GroupCommand, Priority(2)]
            public Task ExecuteGroupAsync(CommandContext ctx,
                                         [Description("Users to block.")] params DiscordUser[] users)
                => this.AddAsync(ctx, null, users);

            [GroupCommand, Priority(1)]
            public Task ExecuteGroupAsync(CommandContext ctx,
                                         [Description("Reason (max 60 chars).")] string reason,
                                         [Description("Users to block.")] params DiscordUser[] users)
                => this.AddAsync(ctx, reason, users);

            [GroupCommand, Priority(0)]
            public Task ExecuteGroupAsync(CommandContext ctx,
                                         [Description("Users to block.")] DiscordUser user,
                                         [RemainingText, Description("Reason (max 60 chars).")] string reason)
                => this.AddAsync(ctx, reason, user);

            #region COMMAND_BLOCKED_USERS_ADD

            [Command("add"), Priority(2)]
            [Description("Add users to blocked users list.")]
            [Aliases("+", "a", "block", "<", "<<", "+=")]
            [UsageExampleArgs("@Someone", "@Someone Troublemaker", "123123123123123", "@Someone 123123123123123", "\"This is some reason\" @Someone 123123123123123")]
            public Task AddAsync(CommandContext ctx,
                              [Description("Users to block.")] params DiscordUser[] users)
              => this.AddAsync(ctx, null, users);

            [Command("add"), Priority(1)]
            public async Task AddAsync(CommandContext ctx,
                                      [Description("Reason (max 60 characters).")] string reason,
                                      [Description("Users to block.")] params DiscordUser[] users)
            {
                if (reason?.Length >= 60)
                    throw new InvalidCommandUsageException("Reason cannot exceed 60 characters");

                if (users is null || !users.Any())
                    throw new InvalidCommandUsageException("Missing users to block.");

                var sb = new StringBuilder();
                using (var dc = this.Database.CreateContext())
                {
                    foreach (var user in users)
                    {
                        if (this.Shared.BlockedUsers.Contains(user.Id))
                        {
                            sb.AppendLine($"Error: {user.ToString()} is already blocked!");
                            continue;
                        }

                        if (!this.Shared.BlockedUsers.Add(user.Id))
                        {
                            sb.AppendLine($"Error: Failed to add {user.ToString()} to blocked users list!");
                            continue;
                        }

                        dc.BlockedUsers.Add(new DatabaseBlockedUser
                        {
                            UserId = user.Id,
                            Reason = reason
                        });
                    }

                    await dc.SaveChangesAsync();
                }

                if (sb.Length > 0)
                    await this.InformOfFailureAsync(ctx, $"Action finished with warnings/errors:\n\n{sb.ToString()}");
                else
                    await this.InformAsync(ctx, $"Blocked all given users.", important: false);
            }

            [Command("add"), Priority(0)]
            public Task AddAsync(CommandContext ctx,
                                [Description("Users to block.")] DiscordUser user,
                                [RemainingText, Description("Reason (max 60 chars).")] string reason)
                => this.AddAsync(ctx, reason, user);

            #endregion COMMAND_BLOCKED_USERS_ADD

            #region COMMAND_BLOCKED_USERS_DELETE

            [Command("delete")]
            [Description("Remove users from blocked users list.")]
            [Aliases("-", "remove", "rm", "del", "unblock", ">", ">>", "-=")]
            [UsageExampleArgs("@Someone", "123123123123123", "@Someone 123123123123123")]
            public async Task DeleteAsync(CommandContext ctx, [Description("Users to unblock.")] params DiscordUser[] users)
            {
                if (users is null || !users.Any())
                    throw new InvalidCommandUsageException("Missing users to block.");

                var sb = new StringBuilder();
                using (var dc = this.Database.CreateContext())
                {
                    foreach (var user in users)
                    {
                        if (!this.Shared.BlockedUsers.Contains(user.Id))
                        {
                            sb.AppendLine($"Warning: {user.ToString()} is not blocked!");
                            continue;
                        }

                        if (!this.Shared.BlockedUsers.TryRemove(user.Id))
                        {
                            sb.AppendLine($"Error: Failed to remove {user.ToString()} from blocked users list!");
                            continue;
                        }

                        dc.BlockedUsers.Remove(new DatabaseBlockedUser { UserId = user.Id });
                    }

                    await dc.SaveChangesAsync();
                }

                if (sb.Length > 0)
                    await this.InformOfFailureAsync(ctx, $"Action finished with warnings/errors:\n\n{sb.ToString()}");
                else
                    await this.InformAsync(ctx, $"Unblocked all given users.", important: false);
            }

            #endregion COMMAND_BLOCKED_USERS_DELETE

            #region COMMAND_BLOCKED_USERS_LIST

            [Command("list")]
            [Description("List all blocked users.")]
            [Aliases("ls", "l", "print")]
            public async Task ListAsync(CommandContext ctx)
            {
                List<DatabaseBlockedUser> blocked;
                using (var dc = this.Database.CreateContext())
                    blocked = await dc.BlockedUsers.ToListAsync();

                var lines = new List<string>();
                foreach (var usr in blocked)
                {
                    try
                    {
                        var user = await ctx.Client.GetUserAsync(usr.UserId);
                        lines.Add($"{user.ToString()} ({Formatter.Italic(usr.Reason ?? "No reason provided.")})");
                    } catch (NotFoundException)
                    {
                        this.Shared.LogProvider.Log(LogLevel.Debug, $"Removed 404 blocked user with ID {usr.UserId}");
                        using (var dc = this.Database.CreateContext())
                        {
                            dc.BlockedUsers.Remove(new DatabaseBlockedUser { UserIdDb = usr.UserIdDb });
                            await dc.SaveChangesAsync();
                        }
                    }
                }

                if (!lines.Any())
                    throw new CommandFailedException("No blocked users registered!");

                await ctx.SendCollectionInPagesAsync("Blocked users (in database):", lines, line => line, this.ModuleColor, 5);
            }

            #endregion COMMAND_BLOCKED_USERS_LIST
        }
    }
}

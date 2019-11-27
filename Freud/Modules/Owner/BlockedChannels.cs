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
        [Group("blockedchannels"), NotBlocked]
        [Description("Manipulate blocked channels. Bot will not listen for commands in blocked channels or react (either with text or emoji) to messages inside.")]
        [Aliases("bc", "blockedc", "blockchannel", "bchannels", "bchannel", "bchn")]
        [RequirePrivilegedUser]
        public class BlockedChannelsModule : FreudModule
        {
            public BlockedChannelsModule(SharedData shared, DatabaseContextBuilder dcb)
                : base(shared, dcb)
            {
                this.ModuleColor = DiscordColor.NotQuiteBlack;
            }

            [GroupCommand, Priority(3)]
            public Task ExecuteGroupAsync(CommandContext ctx)
                => this.ListAsync(ctx);

            [GroupCommand, Priority(2)]
            public Task ExecuteGroupAsync(CommandContext ctx,
                                         [Description("Channels to block.")] params DiscordChannel[] channels)
                => this.AddAsync(ctx, null, channels);

            [GroupCommand, Priority(1)]
            public Task ExecuteGroupAsync(CommandContext ctx,
                                         [Description("Reason (max 60 chars).")] string reason,
                                         [Description("Channels to block.")] params DiscordChannel[] channels)
                => this.AddAsync(ctx, reason, channels);

            [GroupCommand, Priority(0)]
            public Task ExecuteGroupAsync(CommandContext ctx,
                                         [Description("Channels to block.")] DiscordChannel channel,
                                         [RemainingText, Description("Reason (max 60 chars).")] string reason)
                => this.AddAsync(ctx, reason, channel);

            #region COMMAND_BLOCKED_CHANNELS_ADD

            [Command("add"), Priority(2)]
            [Description("Add channel to blocked channels list.")]
            [Aliases("+", "a", "block", "<", "<<", "+=")]
            [UsageExampleArgs("#channel", "#channel Some reason", "123123123123123", "#channel 123123123123123", "\"This is some reason\" #channel 123123123123123")]
            public Task AddAsync(CommandContext ctx,
                                [Description("Channels to block.")] params DiscordChannel[] channels)
                => this.AddAsync(ctx, null, channels);

            [Command("add"), Priority(1)]
            public async Task AddAsync(CommandContext ctx,
                                      [Description("Reason (max 60 chars).")] string reason,
                                      [Description("Channels to block.")] params DiscordChannel[] channels)
            {
                if (reason?.Length >= 60)
                    throw new InvalidCommandUsageException("Reason cannot exceed 60 characters");

                if (channels is null || !channels.Any())
                    throw new InvalidCommandUsageException("Missing channels to block.");

                var sb = new StringBuilder();
                using (var dc = this.Database.CreateContext())
                {
                    foreach (var channel in channels)
                    {
                        if (this.Shared.BlockedChannels.Contains(channel.Id))
                        {
                            sb.AppendLine($"Error: {channel.ToString()} is already blocked!");
                            continue;
                        }

                        if (!this.Shared.BlockedChannels.Add(channel.Id))
                        {
                            sb.AppendLine($"Error: Failed to add {channel.ToString()} to blocked users list!");
                            continue;
                        }

                        dc.BlockedChannels.Add(new DatabaseBlockedChannel
                        {
                            ChannelId = channel.Id,
                            Reason = reason
                        });
                    }

                    await dc.SaveChangesAsync();
                }

                if (sb.Length > 0)
                    await this.InformOfFailureAsync(ctx, $"Action finished with warnings/errors:\n\n{sb.ToString()}");
                else
                    await this.InformAsync(ctx, "Blocked all given channels.", important: false);
            }

            [Command("add"), Priority(0)]
            public Task AddAsync(CommandContext ctx,
                                [Description("Channel to block.")] DiscordChannel channel,
                                [RemainingText, Description("Reason (max 60 chars).")] string reason)
                => this.AddAsync(ctx, reason, channel);

            #endregion COMMAND_BLOCKED_CHANNELS_ADD

            #region COMMAND_BLOCKED_CHANNELS_DELETE

            [Command("delete")]
            [Description("Remove channel from blocked channels list.")]
            [Aliases("-", "remove", "rm", "del", "unblock", ">", ">>", "-=")]
            [UsageExampleArgs("#channel", "123123123123123", "#channel1 #channel2 123123123123123")]
            public async Task DeleteAsync(CommandContext ctx, [Description("Channels to unblock.")] params DiscordChannel[] channels)
            {
                if (channels is null || !channels.Any())
                    throw new InvalidCommandUsageException("Missing channels to block.");

                var sb = new StringBuilder();
                using (var dc = this.Database.CreateContext())
                {
                    foreach (var channel in channels)
                    {
                        if (!this.Shared.BlockedChannels.Contains(channel.Id))
                        {
                            sb.AppendLine($"Warning: {channel.ToString()} is not blocked!");
                            continue;
                        }

                        if (!this.Shared.BlockedChannels.TryRemove(channel.Id))
                        {
                            sb.AppendLine($"Error: Failed to remove {channel.ToString()} from blocked channels list!");
                            continue;
                        }

                        dc.BlockedChannels.Remove(new DatabaseBlockedChannel { ChannelId = channel.Id });
                    }

                    await dc.SaveChangesAsync();
                }

                if (sb.Length > 0)
                    await this.InformOfFailureAsync(ctx, $"Action finished with warnings/errors:\n\n{sb.ToString()}");
                else
                    await this.InformAsync(ctx, "Unlocked all given channels.", important: false);
            }

            #endregion COMMAND_BLOCKED_CHANNELS_DELETE

            #region COMMAND_BLOCKED_CHANNELS_LIST

            [Command("list")]
            [Description("List all blocked channels.")]
            [Aliases("ls", "l", "print")]
            public async Task ListAsync(CommandContext ctx)
            {
                List<DatabaseBlockedChannel> blocked;
                using (var dc = this.Database.CreateContext())
                    blocked = await dc.BlockedChannels.ToListAsync();

                var lines = new List<string>();
                foreach (var chn in blocked)
                {
                    try
                    {
                        var channel = await ctx.Client.GetChannelAsync(chn.ChannelId);
                        lines.Add($"{channel.ToString()} ({Formatter.Italic(chn.Reason ?? "No reason provided.")}");
                    } catch (NotFoundException)
                    {
                        this.Shared.LogProvider.Log(LogLevel.Debug, $"Removed 404 blocked channel with ID {chn.ChannelId}");
                        this.Shared.BlockedChannels.TryRemove(chn.ChannelId);
                        using (var dc = this.Database.CreateContext())
                        {
                            dc.BlockedChannels.Remove(new DatabaseBlockedChannel { ChannelIdDb = chn.ChannelIdDb });
                            await dc.SaveChangesAsync();
                        }
                    } catch (UnauthorizedException)
                    {
                        this.Shared.LogProvider.Log(LogLevel.Debug, $"Removed 403 blocked channel with ID {chn.ChannelId}");
                        this.Shared.BlockedChannels.TryRemove(chn.ChannelId);
                        using (var dc = this.Database.CreateContext())
                        {
                            dc.BlockedChannels.Remove(new DatabaseBlockedChannel { ChannelIdDb = chn.ChannelIdDb });
                            await dc.SaveChangesAsync();
                        }
                    }
                }

                if (!lines.Any())
                    throw new CommandFailedException("No blocked channels registered!");

                await ctx.SendCollectionInPagesAsync("", lines, line => line, this.ModuleColor, 5);
            }

            #endregion COMMAND_BLOCKED_CHANNELS_LIST
        }
    }
}

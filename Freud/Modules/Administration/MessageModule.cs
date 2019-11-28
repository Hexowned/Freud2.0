#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.EventHandling;
using Freud.Common.Attributes;
using Freud.Common.Configuration;
using Freud.Database.Db;
using Freud.Exceptions;
using Freud.Extensions;
using Freud.Extensions.Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Administration
{
    [Group("message"), Module(ModuleType.Administration), NotBlocked]
    [Description("Commands for manipulating messages.")]
    [Aliases("m", "msg", "msgs", "messages")]
    [Cooldown(3, 5, CooldownBucketType.Channel)]
    public partial class MessageModule : FreudModule
    {
        public MessageModule(SharedData shared, DatabaseContextBuilder dcb)
            : base(shared, dcb)
        {
            this.ModuleColor = DiscordColor.Azure;
        }

        #region COMMAND_MESSAGES_ATTACHMENTS

        [Command("attachments")]
        [Description("View all message attachments. If the message is not provided, scans the last sent message before command invocation.")]
        [Aliases("a", "files", "la")]
        [UsageExampleArgs("408226948855234561")]
        public async Task ListAttachmentsAsync(CommandContext ctx,
                                              [Description("Message.")] DiscordMessage message = null)
        {
            message = message ?? (await ctx.Channel.GetMessagesBeforeAsync(ctx.Channel.LastMessageId, 1))?.FirstOrDefault();

            if (message is null)
                throw new CommandFailedException("Cannot retrieve the message!");

            var emb = new DiscordEmbedBuilder
            {
                Title = "Attachments:",
                Color = this.ModuleColor
            };
            foreach (var attachment in message.Attachments)
                emb.AddField($"{attachment.FileName} ({attachment.FileSize} bytes)", attachment.Url);

            await ctx.RespondAsync(embed: emb.Build());
        }

        #endregion COMMAND_MESSAGES_ATTACHMENTS

        #region COMMAND_MESSAGES_FLAG

        [Command("flag")]
        [Description("Flags the message given by ID for deletion vote. If the message is not provided, flags the last sent message before command invocation.")]
        [Aliases("f")]
        [UsageExampleArgs("408226948855234561")]
        [RequireBotPermissions(Permissions.ManageMessages)]
        [Cooldown(1, 60, CooldownBucketType.User)]
        public async Task FlagMessageAsync(CommandContext ctx,
                                          [Description("Message.")] DiscordMessage msg = null,
                                          [Description("Voting timespan.")] TimeSpan? timespan = null)
        {
            msg = msg ?? (await ctx.Channel.GetMessagesBeforeAsync(ctx.Channel.LastMessageId, 1))?.FirstOrDefault();

            if (msg is null)
                throw new CommandFailedException("Cannot retrieve the message!");

            if (timespan?.TotalSeconds < 5 || timespan?.TotalMinutes > 5)
                throw new InvalidCommandUsageException("Timespan cannot be greater than 5 minutes or lower than 5 seconds.");

            IEnumerable<PollEmoji> res = await msg.DoPollAsync(new[] { StaticDiscordEmoji.ArrowUp, StaticDiscordEmoji.ArrowDown }, PollBehaviour.Default, timeout: timespan ?? TimeSpan.FromMinutes(1));
            var votes = res.ToDictionary(pe => pe.Emoji, pe => pe.Voted.Count);
            if (votes.GetValueOrDefault(StaticDiscordEmoji.ArrowDown) > 2 * votes.GetValueOrDefault(StaticDiscordEmoji.ArrowUp))
            {
                string sanitized = FormatterExtensions.Spoiler(FormatterExtensions.StripMarkdown(msg.Content));
                await msg.DeleteAsync();
                await ctx.RespondAsync($"{msg.Author.Mention} said: {sanitized}");
            } else
            {
                await this.InformOfFailureAsync(ctx, "Not enough downvotes required for deletion.");
            }
        }

        #endregion COMMAND_MESSAGES_FLAG

        #region COMMAND_MESSAGES_LISTPINNED

        [Command("listpinned")]
        [Description("List pinned messages in this channel.")]
        [Aliases("lp", "listpins", "listpin", "pinned")]
        public async Task ListPinnedMessagesAsync(CommandContext ctx)
        {
            var pinned = await ctx.Channel.GetPinnedMessagesAsync();

            if (!pinned.Any())
            {
                await this.InformOfFailureAsync(ctx, "No pinned messages in this channel");
                return;
            }

            var pages = pinned.Select(m => new Page(
                $"Author: {Formatter.Bold(m.Author.Username)} {m.CreationTimestamp.ToUtcTimestamp()}",
                GetFirstEmbedOrDefaultAsBuilder(m)
            ));

            await ctx.Client.GetInteractivity().SendPaginatedMessageAsync(ctx.Channel, ctx.User, pages);

            DiscordEmbedBuilder GetFirstEmbedOrDefaultAsBuilder(DiscordMessage m)
            {
                var em = m.Embeds.FirstOrDefault();
                if (!(em is null))
                    return new DiscordEmbedBuilder(m.Embeds.First());
                return new DiscordEmbedBuilder
                {
                    Title = "Jump to",
                    Description = m.Content ?? Formatter.Italic("Empty message."),
                    Url = m.JumpLink.ToString()
                };
            }
        }

        #endregion COMMAND_MESSAGES_LISTPINNED

        #region COMMAND_MESSAGES_MODIFY

        [Command("modify")]
        [Description("Modify the given message.")]
        [Aliases("edit", "mod", "e", "m")]
        [UsageExampleArgs("408226948855234561 modified text")]
        [RequirePermissions(Permissions.ManageMessages)]
        public async Task ModifyMessageAsync(CommandContext ctx,
                                            [Description("Message.")] DiscordMessage message,
                                            [RemainingText, Description("New content.")] string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new CommandFailedException("Missing new message content!");

            await message.ModifyAsync(content);
            await this.InformAsync(ctx, important: false);
        }

        #endregion COMMAND_MESSAGES_MODIFY

        #region COMMAND_MESSAGES_PIN

        [Command("pin")]
        [Description("Pins the message given by ID. If the message is not provided, pins the last sent message before command invocation.")]
        [Aliases("p")]
        [UsageExampleArgs("408226948855234561")]
        [RequirePermissions(Permissions.ManageMessages)]
        public async Task PinMessageAsync(CommandContext ctx,
                                         [Description("Message.")] DiscordMessage message = null)
        {
            message = message ?? (await ctx.Channel.GetMessagesBeforeAsync(ctx.Channel.LastMessageId, 1))?.FirstOrDefault();

            if (message is null)
                throw new CommandFailedException("Cannot retrieve the message!");

            await message.PinAsync();
        }

        #endregion COMMAND_MESSAGES_PIN

        #region COMMAND_MESSAGES_UNPIN

        [Command("unpin"), Priority(1)]
        [Description("Unpins the message at given index (starting from 1) or message ID. If the index is not given, unpins the most recent one.")]
        [Aliases("up")]
        [UsageExampleArgs("12345645687955", "10")]
        [RequirePermissions(Permissions.ManageMessages)]
        public async Task UnpinMessageAsync(CommandContext ctx,
                                           [Description("Message.")] DiscordMessage message)
        {
            await message.UnpinAsync();
            await this.InformAsync(ctx, "Removed the specified pin.", important: false);
        }

        [Command("unpin"), Priority(0)]
        public async Task UnpinMessageAsync(CommandContext ctx,
                                           [Description("Index (starting from 1).")] int index = 1)
        {
            var pinned = await ctx.Channel.GetPinnedMessagesAsync();

            if (index < 1 || index > pinned.Count)
                throw new CommandFailedException($"Invalid index (must be in range [1, {pinned.Count}]!");

            await pinned.ElementAt(index - 1).UnpinAsync();
            await this.InformAsync(ctx, "Removed the specified pin.", important: false);
        }

        #endregion COMMAND_MESSAGES_UNPIN

        #region COMMAND_MESSAGES_UNPINALL

        [Command("unpinall")]
        [Description("Unpins all pinned messages in this channel.")]
        [Aliases("upa")]
        [RequirePermissions(Permissions.ManageMessages)]
        public async Task UnpinAllMessagesAsync(CommandContext ctx)
        {
            var pinned = await ctx.Channel.GetPinnedMessagesAsync();

            int failed = 0;
            foreach (var m in pinned)
            {
                try
                {
                    await m.UnpinAsync();
                } catch
                {
                    failed++;
                }
            }

            if (failed > 0)
                await this.InformOfFailureAsync(ctx, $"Failed to unpin {failed} messages!");
            else
                await this.InformAsync(ctx, "Successfully unpinned all messages in this channel", important: false);
        }

        #endregion COMMAND_MESSAGES_UNPINALL
    }

    public partial class MessageModule
    {
        [Group("delete"), UsageInteractivity]
        [Description("Deletes messages from the current channel. Group call deletes given amount of most recent messages.")]
        [Aliases("-", "prune", "del", "d")]
        [UsageExampleArgs("10", "10 Cleaning spam")]
        [RequirePermissions(Permissions.ManageMessages), RequireUserPermissions(Permissions.Administrator)]
        public class MessageDeleteModule : FreudModule
        {
            public MessageDeleteModule(SharedData shared, DatabaseContextBuilder dcb)
                : base(shared, dcb)
            {
                this.ModuleColor = DiscordColor.Azure;
            }

            [GroupCommand]
            public async Task DeleteMessagesAsync(CommandContext ctx,
                                                 [Description("Amount.")] int amount = 5,
                                                 [RemainingText, Description("Reason.")] string reason = null)
            {
                if (amount < 1 || amount > 10000)
                    throw new CommandFailedException("Invalid number of messages to delete (must be in range [1, 10000].");

                if (amount > 100 && !await ctx.WaitForBoolReplyAsync($"Are you sure you want to delete {Formatter.Bold(amount.ToString())} messages from this channel?"))
                    return;

                var msgs = await ctx.Channel.GetMessagesAsync(amount);
                if (!msgs.Any())
                    throw new CommandFailedException("None of the messages in the given range match your description.");

                await ctx.Channel.DeleteMessagesAsync(msgs, ctx.BuildInvocationDetailsString(reason));
            }

            #region COMMAND_MESSAGES_DELETE_AFTER

            [Command("after")]
            [Description("Deletes given amount messages after a specified message ID.")]
            [Aliases("aft", "af")]
            [UsageExampleArgs("4022123456789132 20 Cleaning spam")]
            public async Task DeleteMessagesAfterAsync(CommandContext ctx,
                                                      [Description("Message after which to delete.")] DiscordMessage message,
                                                      [Description("Amount.")] int amount = 5,
                                                      [RemainingText, Description("Reason.")] string reason = null)
            {
                if (amount < 1 || amount > 100)
                    throw new CommandFailedException("Cannot delete less than 1 and more than 100 messages at a time.");

                var msgs = await ctx.Channel.GetMessagesAfterAsync(message.Id, amount);
                await ctx.Channel.DeleteMessagesAsync(msgs, ctx.BuildInvocationDetailsString(reason));
            }

            #endregion COMMAND_MESSAGES_DELETE_AFTER

            #region COMMAND_MESSAGES_DELETE_BEFORE

            [Command("before")]
            [Description("Deletes given amount messages before a specified message ID.")]
            [Aliases("bef", "bf")]
            [UsageExampleArgs("4022123456789132 20 Cleaning spam")]
            public async Task DeleteMessagesBeforeAsync(CommandContext ctx,
                                                       [Description("Message before which to delete.")] DiscordMessage message,
                                                       [Description("Amount.")] int amount = 5,
                                                       [RemainingText, Description("Reason.")] string reason = null)
            {
                if (amount < 1 || amount > 100)
                    throw new CommandFailedException("Cannot delete less than 1 and more than 100 messages at a time.");

                var msgs = await ctx.Channel.GetMessagesBeforeAsync(message.Id, amount);
                await ctx.Channel.DeleteMessagesAsync(msgs, ctx.BuildInvocationDetailsString(reason));
            }

            #endregion COMMAND_MESSAGES_DELETE_BEFORE

            #region COMMAND_MESSAGES_DELETE_FROM

            [Command("from"), Priority(1)]
            [Description("Deletes given amount of most recent messages from the given member.")]
            [Aliases("f", "frm")]
            [UsageExampleArgs("@Someone 10 Cleaning spam", "10 @Someone Cleaning spam")]
            public async Task DeleteMessagesFromUserAsync(CommandContext ctx,
                                                         [Description("User whose messages to delete.")] DiscordMember member,
                                                         [Description("Message range.")] int amount = 5,
                                                         [RemainingText, Description("Reason.")] string reason = null)
            {
                if (amount <= 0 || amount > 10000)
                    throw new CommandFailedException("Invalid number of messages to delete (must be in range [1, 10000].");

                var msgs = await ctx.Channel.GetMessagesAsync(amount);

                await ctx.Channel.DeleteMessagesAsync(msgs.Where(m => m.Author.Id == member.Id), ctx.BuildInvocationDetailsString(reason));
            }

            [Command("from"), Priority(0)]
            public Task DeleteMessagesFromUserAsync(CommandContext ctx,
                                                   [Description("Amount.")] int amount,
                                                   [Description("User.")] DiscordMember member,
                                                   [RemainingText, Description("Reason.")] string reason = null)
                => this.DeleteMessagesFromUserAsync(ctx, member, amount, reason);

            #endregion COMMAND_MESSAGES_DELETE_FROM

            #region COMMAND_MESSAGES_DELETE_REACTIONS

            [Command("reactions")]
            [Description("Deletes all reactions from the given message.")]
            [Aliases("react", "re")]
            [UsageExampleArgs("408226948855234561")]
            public async Task DeleteReactionsAsync(CommandContext ctx,
                                                  [Description("Message.")] DiscordMessage message = null,
                                                  [RemainingText, Description("Reason.")] string reason = null)
            {
                message = message ?? (await ctx.Channel.GetMessagesBeforeAsync(ctx.Channel.LastMessageId, 1)).FirstOrDefault();
                if (message is null)
                    throw new CommandFailedException("Cannot find the specified message.");

                await message.DeleteAllReactionsAsync(ctx.BuildInvocationDetailsString(reason));
                await this.InformAsync(ctx, important: false);
            }

            #endregion COMMAND_MESSAGES_DELETE_REACTIONS

            #region COMMAND_MESSAGES_DELETE_REGEX

            [Command("regex"), Priority(1)]
            [Description("Deletes given amount of most-recent messages that match a given regular expression withing a given message amount.")]
            [Aliases("r", "rgx", "regexp", "reg")]
            [UsageExampleArgs("s+p+a+m+ 10 Cleaning spam", "10 s+p+a+m+ Cleaning spam")]
            public async Task DeleteMessagesFromRegexAsync(CommandContext ctx,
                                                          [Description("Pattern (Regex).")] string pattern,
                                                          [Description("Amount.")] int amount = 100,
                                                          [RemainingText, Description("Reason.")] string reason = null)
            {
                if (amount <= 0 || amount > 100)
                    throw new CommandFailedException("Invalid number of messages to delete (must be in range [1, 100].");

                if (!pattern.TryParseRegex(out Regex regex))
                    throw new CommandFailedException("Regex pattern specified is not valid!");

                var msgs = await ctx.Channel.GetMessagesBeforeAsync(ctx.Channel.LastMessageId, amount);

                await ctx.Channel.DeleteMessagesAsync(msgs.Where(m => !string.IsNullOrWhiteSpace(m.Content) && regex.IsMatch(m.Content)), ctx.BuildInvocationDetailsString(reason));
            }

            [Command("regex"), Priority(0)]
            public Task DeleteMessagesFromRegexAsync(CommandContext ctx,
                                                    [Description("Amount.")] int amount,
                                                    [Description("Pattern (Regex).")] string pattern,
                                                    [RemainingText, Description("Reason.")] string reason = null)
                => this.DeleteMessagesFromRegexAsync(ctx, pattern, amount, reason);

            #endregion COMMAND_MESSAGES_DELETE_REGEX
        }
    }
}

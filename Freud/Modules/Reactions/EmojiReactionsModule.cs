﻿#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Freud.Common.Attributes;
using Freud.Common.Collections;
using Freud.Database.Db;
using Freud.Database.Db.Entities;
using Freud.Exceptions;
using Freud.Extensions;
using Freud.Extensions.Discord;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Reactions
{
    [Group("emojireaction"), Module(ModuleType.Reactions), NotBlocked]
    [Description("Orders a bot to react with given emoji to a message containing a trigger word inside (guild specific)." +
                 " If invoked without subcommands, adds a new emoji reaction to a given trigger word list. Note: Trigger words can be regular expressions (use ``emojireaction addregex`` command).")]
    [Aliases("ereact", "er", "emojir", "emojireactions")]
    [UsageExampleArgs(":smile: haha laughing")]
    [RequirePermissions(Permissions.ManageGuild)]
    [Cooldown(3, 5, CooldownBucketType.Guild)]
    public class EmojiReactionsModule : FreudModule
    {
        public EmojiReactionsModule(SharedData shared, DatabaseContextBuilder db)
            : base(shared, db)
        {
            this.ModuleColor = DiscordColor.VeryDarkGray;
        }

        [GroupCommand, Priority(2)]
        public Task ExecuteGroupAsync(CommandContext ctx)
            => this.ListAsync(ctx);

        [GroupCommand, Priority(1)]
        public Task ExecuteGroupAsync(CommandContext ctx,
                                     [Description("Emoji to send.")] DiscordEmoji emoji,
                                     [RemainingText, Description("Trigger word list.")] params string[] triggers)
            => this.AddEmojiReactionAsync(ctx, emoji, false, triggers);

        [GroupCommand, Priority(0)]
        public Task ExecuteGroupAsync(CommandContext ctx,
                                     [Description("Trigger word (case-insensitive).")] string trigger,
                                     [Description("Emoji to send.")] DiscordEmoji emoji)
            => this.AddEmojiReactionAsync(ctx, emoji, false, trigger);

        #region COMMAND_EMOJI_REACTIONS_ADD

        [Command("add"), Priority(1)]
        [Description("Add an emoji reaction to a guild reaction list.")]
        [Aliases("+", "new", "a", "+=", "<", "<<")]
        [UsageExampleArgs(":smile: haha", "haha :smile:")]
        public Task AddAsync(CommandContext ctx,
                            [Description("Emoji to send.")] DiscordEmoji emoji,
                            [RemainingText, Description("Trigger word list (case-sensitive).")] params string[] triggers)
            => this.AddEmojiReactionAsync(ctx, emoji, false, triggers);

        [Command("add"), Priority(0)]
        public Task AddAsync(CommandContext ctx,
                            [Description("Trigger word (case-sensitive)")] string trigger,
                            [Description("Emoji to send")] DiscordEmoji emoji)
            => this.AddEmojiReactionAsync(ctx, emoji, false, trigger);

        #endregion COMMAND_EMOJI_REACTIONS_ADD

        #region COMMAND_EMOJI_REACTIONS_ADD_REGEX

        [Command("addregex"), Priority(1)]
        [Description("Add an emoji reaction triggered by a regex to the guild reaction list.")]
        [Aliases("+r", "+regex", "+regexp", "+rgx", "newregex", "addrgx", "+=r", "<r", "<<r")]
        [UsageExampleArgs(":smile: (ha)+", "(ha)+ :smile:")]
        public Task AddRegexAsync(CommandContext ctx,
                                 [Description("Emoji to send.")] DiscordEmoji emoji,
                                 [RemainingText, Description("Trigger world list (case-sensitive).")] params string[] triggers)
            => this.AddEmojiReactionAsync(ctx, emoji, true, triggers);

        [Command("addregex"), Priority(0)]
        public Task AddRegexAsync(CommandContext ctx,
                                 [Description("Trigger word (case-sensitive)")] string trigger,
                                 [Description("Emoji to send.")] DiscordEmoji emoji)
            => this.AddEmojiReactionAsync(ctx, emoji, true, trigger);

        #endregion COMMAND_EMOJI_REACTIONS_ADD_REGEX

        #region COMMAND_EMOJI_REACTIONS_DELETE

        [Command("delete"), Priority(2)]
        [Description("Remove emoji reactions for given trigger words.")]
        [Aliases("-", "remove", "del", "rm", "d", "-=", ">", ">>")]
        [UsageExampleArgs("haha sometrigger", "5", "5 4", ":joy:")]
        public async Task DeleteAsync(CommandContext ctx, [Description("Emoji to remove reactions for")] DiscordEmoji emoji)
        {
            if (!this.Shared.EmojiReactions.TryGetValue(ctx.Guild.Id, out var ereactions))
                throw new CommandFailedException("The emoji reactions are not registered");

            string ename = emoji.GetDiscordName();
            if (ereactions.RemoveWhere(er => er.Response == ename) == 0)
                throw new CommandFailedException("No such reactions were found!");

            using (var dc = this.Database.CreateContext())
            {
                dc.EmojiReactions.RemoveRange(dc.EmojiReactions.Where(er => er.GuildId == ctx.Guild.Id && er.Reaction == ename));
                await dc.SaveChangesAsync();
            }

            var logch = this.Shared.GetLogChannelForGuild(ctx.Client, ctx.Guild);
            if (!(logch is null))
            {
                var emb = new DiscordEmbedBuilder
                {
                    Title = "Several emoji reactions have been deleted",
                    Color = this.ModuleColor
                };
                emb.AddField("User responsible", ctx.User.Mention, inline: true);
                emb.AddField("Invoked in", ctx.Channel.Mention, inline: true);
                emb.AddField("Removed reaction for emoji", emoji, inline: true);

                await logch.SendMessageAsync(embed: emb.Build());
            }

            await this.InformAsync(ctx, $"Removed reactions that contain emoji: {emoji}", important: false);
        }

        [Command("delete"), Priority(1)]
        public async Task DeleteAsync(CommandContext ctx, [Description("IDs of the reactions to remove.")] params int[] ids)
        {
            if (ids is null || !ids.Any())
                throw new InvalidCommandUsageException("You need to specify at least one ID to remove.");

            if (!this.Shared.EmojiReactions.TryGetValue(ctx.Guild.Id, out var ereactions))
                throw new CommandFailedException("There are no emoji reactions registered.");

            var sb = new StringBuilder();

            using (var dc = this.Database.CreateContext())
            {
                foreach (int id in ids)
                {
                    if (!ereactions.Any(er => er.Id == id))
                    {
                        sb.AppendLine($"Note: Reaction with ID {id} does not exist in this guild.");
                        continue;
                    } else
                    {
                        dc.EmojiReactions.Remove(new DatabaseEmojiReaction { Id = id, GuildId = ctx.Guild.Id });
                    }
                }

                await dc.SaveChangesAsync();
            }

            int count = ereactions.RemoveWhere(er => ids.Contains(er.Id));

            if (count > 0)
            {
                var logchn = this.Shared.GetLogChannelForGuild(ctx.Client, ctx.Guild);
                if (!(logchn is null))
                {
                    var emb = new DiscordEmbedBuilder
                    {
                        Title = "Several emoji reactions have been deleted",
                        Color = this.ModuleColor
                    };
                    emb.AddField("User responsible", ctx.User.Mention, inline: true);
                    emb.AddField("Invoked in", ctx.Channel.Mention, inline: true);
                    emb.AddField("Removed successfully", $"{count} reactions", inline: true);
                    emb.AddField("IDs attempted to be removed", string.Join(", ", ids));
                    if (sb.Length > 0)
                        emb.AddField("With errors", sb.ToString());
                    await logchn.SendMessageAsync(embed: emb.Build()).ConfigureAwait(false);
                }
            }

            if (sb.Length > 0)
                await this.InformOfFailureAsync(ctx, $"Action finished with the following notes/warnings:\n\n{sb.ToString()}");
            else
                await this.InformAsync(ctx, $"Removed {count} reactions matching given IDs.", important: false);
        }

        [Command("delete"), Priority(0)]
        public async Task DeleteAsync(CommandContext ctx, [RemainingText, Description("Trigger words to remove.")] params string[] triggers)
        {
            if (triggers is null || !triggers.Any())
                throw new InvalidCommandUsageException("Missing trigger words!");

            if (!this.Shared.EmojiReactions.TryGetValue(ctx.Guild.Id, out var ereactions))
                throw new CommandFailedException("This guild has no emoji reactions registered.");

            var erIds = new List<int>();

            var sb = new StringBuilder();
            triggers = triggers.Select(t => t.ToLowerInvariant()).ToArray();
            foreach (string trigger in triggers)
            {
                if (trigger.IsValidRegex())
                {
                    sb.AppendLine($"Error: Trigger {Formatter.Bold(trigger)} is not a valid regular expression.");
                    continue;
                }

                IEnumerable<EmojiReaction> found = ereactions.Where(er => er.ContainsTriggerPattern(trigger));
                if (!found.Any())
                {
                    sb.AppendLine($"Warrning: Trigger {Formatter.Bold(trigger)} does not exist in the guild.");
                    continue;
                }

                bool success = true;
                foreach (EmojiReaction er in found)
                {
                    success |= er.RemoveTrigger(trigger);
                    erIds.Add(er.Id);
                }

                if (!success)
                {
                    sb.AppendLine($"Warning: Failed to remove some emoji reactions for trigger {Formatter.Bold(trigger)}.");
                    continue;
                }
            }

            using (var dc = this.Database.CreateContext())
            {
                var toUpdate = dc.EmojiReactions.Include(t => t.DbTriggers).AsEnumerable().Where(tr => tr.GuildId == ctx.Guild.Id && erIds.Contains(tr.Id)).ToList();

                foreach (var er in toUpdate)
                {
                    foreach (string trigger in triggers)
                        er.DbTriggers.Remove(new DatabaseEmojiReactionTrigger { ReactionId = er.Id, Trigger = trigger });
                    await dc.SaveChangesAsync();

                    if (er.DbTriggers.Any())
                    {
                        dc.EmojiReactions.Remove(er);
                        await dc.SaveChangesAsync();
                    }
                }
            }

            int count = ereactions.RemoveWhere(er => er.RegexCount == 0);

            if (count > 0)
            {
                var logchn = this.Shared.GetLogChannelForGuild(ctx.Client, ctx.Guild);
                if (!(logchn is null))
                {
                    var emb = new DiscordEmbedBuilder
                    {
                        Title = "Several emoji reactions have been deleted",
                        Color = this.ModuleColor
                    };
                    emb.AddField("User responsible", ctx.User.Mention, inline: true);
                    emb.AddField("Invoked in", ctx.Channel.Mention, inline: true);
                    emb.AddField("Removed successfully", $"{count} reactions", inline: true);
                    emb.AddField("Triggers attempted to be removed", string.Join("\n", triggers));
                    if (sb.Length > 0)
                        emb.AddField("With errors", sb.ToString());
                    await logchn.SendMessageAsync(embed: emb.Build());
                }
            }

            if (sb.Length > 0)
                await this.InformOfFailureAsync(ctx, $"Action finished with the following warnings/errors:\n\n{sb.ToString()}");
            else
                await this.InformAsync(ctx, $"Done! {count} reactions were removed completely.", important: false);
        }

        #endregion COMMAND_EMOJI_REACTIONS_DELETE

        #region COMMAND_EMOJI_REACTIONS_DELETE_ALL

        [Command("deleteall"), UsageInteractivity]
        [Description("Delete all reactions for the current guild.")]
        [Aliases("clear", "da", "c", "ca", "cl", "clearall", ">>>")]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task DeleteAllAsync(CommandContext ctx)
        {
            if (!await ctx.WaitForBoolReplyAsync("Are you sure you want to delete all emoji reactions for this guild?"))
                return;

            if (this.Shared.EmojiReactions.ContainsKey(ctx.Guild.Id))
                if (!this.Shared.EmojiReactions.TryRemove(ctx.Guild.Id, out _))
                    throw new ConcurrentOperationException("Failed to remove emoji reaction collection!");

            using (var dc = this.Database.CreateContext())
            {
                dc.EmojiReactions.RemoveRange(dc.EmojiReactions.Where(er => er.GuildId == ctx.Guild.Id));
                await dc.SaveChangesAsync();
            }

            var logchn = this.Shared.GetLogChannelForGuild(ctx.Client, ctx.Guild);
            if (!(logchn is null))
            {
                var emb = new DiscordEmbedBuilder
                {
                    Title = "All emoji reactions have been deleted",
                    Color = this.ModuleColor
                };
                emb.AddField("User responsible", ctx.User.Mention, inline: true);
                emb.AddField("Invoked in", ctx.Channel.Mention, inline: true);
                await logchn.SendMessageAsync(embed: emb.Build());
            }

            await this.InformAsync(ctx, "Removed all emoji reactions!", important: false);
        }

        #endregion COMMAND_EMOJI_REACTIONS_DELETE_ALL

        #region COMMAND_EMOJI_REACTIONS_FIND

        [Command("find")]
        [Description("Show all emoji reactions that matches the specified trigger.")]
        [Aliases("f")]
        [UsageExampleArgs("hello")]
        public Task ListAsync(CommandContext ctx, [RemainingText, Description("Specific trigger.")] string trigger)
        {
            if (!this.Shared.EmojiReactions.TryGetValue(ctx.Guild.Id, out var ereactions) || !ereactions.Any())
                throw new CommandFailedException("This guild has no text reactions registered.");

            var ers = ereactions.Where(t => t.IsMatch(trigger));
            if (!ers.Any())
                throw new CommandFailedException("None of the reactions respond to such trigger.");

            return ctx.RespondAsync(embed: new DiscordEmbedBuilder
            {
                Title = "Text reaction that matches the trigger",
                Description = string.Join("\n", ers.Select(er => $"{Formatter.InlineCode(er.Id.ToString())} | {DiscordEmoji.FromName(ctx.Client, er.Response)} | {Formatter.InlineCode(string.Join(", ", er.TriggerStrings))}")),
                Color = this.ModuleColor
            }.Build());
        }

        #endregion COMMAND_EMOJI_REACTIONS_FIND

        #region COMMAND_EMOJI_REACTIONS_LIST

        [Command("list")]
        [Description("Show all emoji reactions for this guild.")]
        [Aliases("ls", "l", "print")]
        public async Task ListAsync(CommandContext ctx)
        {
            if (!this.Shared.EmojiReactions.TryGetValue(ctx.Guild.Id, out var ereactions))
                throw new CommandFailedException("No emoji reactions registered for this guild.");

            foreach (EmojiReaction reaction in ereactions)
            {
                try
                {
                    var emoji = DiscordEmoji.FromName(ctx.Client, reaction.Response);
                } catch (ArgumentException)
                {
                    ereactions.RemoveWhere(er => er.Response == reaction.Response);
                    using (var dc = this.Database.CreateContext())
                    {
                        dc.EmojiReactions.RemoveRange(dc.EmojiReactions.Where(er => er.GuildId == ctx.Guild.Id && er.Reaction == reaction.Response));
                        await dc.SaveChangesAsync();
                    }
                }
            }

            // In case some of the reactions are deleted above, this check comes after
            if (!ereactions.Any())
                throw new CommandFailedException("No emoji reactions registered for this guild.");

            await ctx.SendCollectionInPagesAsync("Emoji reactions for this guild", ereactions.OrderBy(er => er.Id),
                er => $"{Formatter.InlineCode($"{er.Id:D4}")} | {DiscordEmoji.FromName(ctx.Client, er.Response)} | {string.Join(", ", er.TriggerStrings)}",
                this.ModuleColor
            );
        }

        #endregion COMMAND_EMOJI_REACTIONS_LIST

        #region HELPER_FUNCTIONS

        private async Task AddEmojiReactionAsync(CommandContext ctx, DiscordEmoji emoji, bool regex, params string[] triggers)
        {
            if (emoji is DiscordGuildEmoji && !ctx.Guild.Emojis.Values.Contains(emoji))
                throw new CommandFailedException("The reaction has to be an emoji from this guild.");

            if (triggers is null || !triggers.Any())
                throw new InvalidCommandUsageException("Missing trigger words!");

            if (!this.Shared.EmojiReactions.ContainsKey(ctx.Guild.Id))
                if (!this.Shared.EmojiReactions.TryAdd(ctx.Guild.Id, new ConcurrentHashSet<EmojiReaction>()))
                    throw new ConcurrentOperationException("Failed to create emoji reaction data struct");

            int id;
            using (var dc = this.Database.CreateContext())
            {
                DatabaseEmojiReaction dber = dc.EmojiReactions.FirstOrDefault(er => er.GuildId == ctx.Guild.Id && er.Reaction == emoji.GetDiscordName());
                if (dber is null)
                {
                    dber = new DatabaseEmojiReaction
                    {
                        GuildId = ctx.Guild.Id,
                        Reaction = emoji.GetDiscordName()
                    };
                    dc.EmojiReactions.Add(dber);
                    await dc.SaveChangesAsync();
                }

                foreach (string trigger in triggers)
                    dber.DbTriggers.Add(new DatabaseEmojiReactionTrigger { ReactionId = dber.Id, Trigger = regex ? trigger : Regex.Escape(trigger) });

                await dc.SaveChangesAsync();
                id = dber.Id;
            }

            var sb = new StringBuilder();
            foreach (string trigger in triggers.Select(t => t.ToLowerInvariant()))
            {
                if (trigger.Length > 120)
                {
                    sb.AppendLine($"Error: Trigger {Formatter.Bold(trigger)} is too long (120 char max).");
                    continue;
                }

                if (regex && !trigger.IsValidRegex())
                {
                    sb.AppendLine($"Error: Trigger {Formatter.Bold(trigger)} is not a valid regular expression!");
                    continue;
                }

                var ereactions = this.Shared.EmojiReactions[ctx.Guild.Id];
                string ename = emoji.GetDiscordName();

                if (ereactions.Where(er => er.ContainsTriggerPattern(trigger)).Any(er => er.Response == ename))
                {
                    sb.AppendLine($"Error: Trigger {Formatter.Bold(trigger)} already exists for this emoji.");
                    continue;
                }

                EmojiReaction reaction = ereactions.FirstOrDefault(tr => tr.Response == ename);
                if (reaction is null)
                {
                    if (!ereactions.Add(new EmojiReaction(id, trigger, ename, isRegex: regex)))
                        throw new CommandFailedException($"Failed to add trigger {Formatter.Bold(trigger)}.");
                } else
                {
                    if (!reaction.AddTrigger(trigger, isRegex: regex))
                        throw new CommandFailedException($"Failed to add trigger {Formatter.Bold(trigger)}.");
                }
            }

            var logchn = this.Shared.GetLogChannelForGuild(ctx.Client, ctx.Guild);
            if (!(logchn is null))
            {
                var emb = new DiscordEmbedBuilder
                {
                    Title = "New emoji reactions added",
                    Color = this.ModuleColor
                };
                emb.AddField("User responsible", ctx.User.Mention, inline: true);
                emb.AddField("Invoked in", ctx.Channel.Mention, inline: true);
                emb.AddField("Reaction", emoji, inline: true);
                emb.AddField("Triggers", string.Join("\n", triggers));

                if (sb.Length > 0)
                    emb.AddField("With errors", sb.ToString());
                await logchn.SendMessageAsync(embed: emb.Build());
            }

            if (sb.Length > 0)
                await this.InformOfFailureAsync(ctx, $"Action finished with following warnings/errors:\n\n{sb.ToString()}");
            else
                await this.InformAsync(ctx, "Successfully added all given emoji reactions.", important: false);
        }

        #endregion HELPER_FUNCTIONS
    }
}

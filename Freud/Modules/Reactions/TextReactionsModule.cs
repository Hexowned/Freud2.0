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
    [Group("textreaction"), Module(ModuleType.Reactions), NotBlocked]
    [Description("Orders a bot to react with given text to a message containing a trigger word inside (guild specific). If invoked without subcommands, adds a new text reaction to a given trigger word. Note: Trigger words can be regular expressions (use ``textreaction addregex`` command). You can also use \"%user%\" inside response and the bot will replace it with mention for the user who triggers the reaction. Text reactions have a one minute cooldown.")]
    [Aliases("treact", "tr", "txtr", "textreactions")]
    [UsageExampleArgs("hello", "\"hi\" \"Hello, %user%!\"")]
    [RequireUserPermissions(Permissions.ManageGuild)]
    [Cooldown(3, 5, CooldownBucketType.Guild)]
    public class TextReactionsModule : FreudModule
    {
        public TextReactionsModule(SharedData shared, DatabaseContextBuilder dcb)
            : base(shared, dcb)
        {
            this.ModuleColor = DiscordColor.DarkGray;
        }

        [GroupCommand, Priority(1)]
        public Task ExecuteGroupAsync(CommandContext ctx)
            => this.ListAsync(ctx);

        [GroupCommand, Priority(0)]
        public Task ExecuteGroupAsync(CommandContext ctx,
                                     [Description("Trigger string (case-sensitive).")] string trigger,
                                     [RemainingText, Description("Response.")] string response)
            => this.AddAsync(ctx, trigger, response);

        #region COMMAND_TEXT_REACTIONS_ADD

        [Command("add")]
        [Description("Add a new text reaction to guild text reaction list.")]
        [Aliases("+", "new", "a", "+=", "<", "<<")]
        [UsageExampleArgs("\"hi\" \"Hello, %user%!\"")]
        public Task AddAsync(CommandContext ctx,
                         [Description("Trigger string (case insensitive).")] string trigger,
                         [RemainingText, Description("Response.")] string response)
         => this.AddTextReactionAsync(ctx, trigger, response, false);

        #endregion COMMAND_TEXT_REACTIONS_ADD

        #region COMMAND_TEXT_REACTIONS_ADD_REGEX

        [Command("addregex")]
        [Description("Add a new text reaction triggered by a regex to guild text reaction list.")]
        [Aliases("+r", "+regex", "+regexp", "+rgx", "newregex", "addrgx", "+=r", "<r", "<<r")]
        [UsageExampleArgs("\"h(i|ey|ello|owdy)\" \"Hello, %user%!\"")]
        public Task AddRegexAsync(CommandContext ctx,
                                [Description("Regex (case insensitive).")] string trigger,
                                [RemainingText, Description("Response.")] string response)
           => this.AddTextReactionAsync(ctx, trigger, response, true);

        #endregion COMMAND_TEXT_REACTIONS_ADD_REGEX

        #region COMMAND_TEXT_REACTIONS_DELETE

        [Command("delete"), Priority(1)]
        [Description("Remove text reaction from guild text reaction list.")]
        [Aliases("-", "remove", "del", "rm", "d", "-=", ">", ">>")]
        [UsageExampleArgs("5", "5 8", "hi")]
        public async Task DeleteAsync(CommandContext ctx,
                                     [Description("IDs of the reactions to remove.")] params int[] ids)
        {
            if (ids is null || !ids.Any())
                throw new InvalidCommandUsageException("You need to specify atleast one ID to remove.");

            if (!this.Shared.TextReactions.TryGetValue(ctx.Guild.Id, out var treactions))
                throw new CommandFailedException("This guild has no text reactions registered.");

            var sb = new StringBuilder();
            foreach (int id in ids)
            {
                if (!treactions.Any(tr => tr.Id == id))
                {
                    sb.AppendLine($"Note: Reaction with ID {id} does not exist in this guild.");
                    continue;
                }
            }

            using (var dc = this.Database.CreateContext())
            {
                dc.TextReactions.RemoveRange(dc.TextReactions.Where(tr => tr.GuildId == ctx.Guild.Id && ids.Contains(tr.Id)));
                await dc.SaveChangesAsync();
            }

            int count = treactions.RemoveWhere(tr => ids.Contains(tr.Id));

            if (count > 0)
            {
                var logchn = this.Shared.GetLogChannelForGuild(ctx.Client, ctx.Guild);
                if (!(logchn is null))
                {
                    var emb = new DiscordEmbedBuilder
                    {
                        Title = "Several text reactions have been deleted",
                        Color = this.ModuleColor
                    };
                    emb.AddField("User responsible", ctx.User.Mention, inline: true);
                    emb.AddField("Invoked in", ctx.Channel.Mention, inline: true);
                    emb.AddField("Removed successfully", $"{count} reactions", inline: true);
                    emb.AddField("IDs attempted to be removed", string.Join(", ", ids));
                    if (sb.Length > 0)
                        emb.AddField("With errors", sb.ToString());
                    await logchn.SendMessageAsync(embed: emb.Build());
                }
            }

            if (sb.Length > 0)
                await this.InformOfFailureAsync(ctx, $"Action finished with following notes/warnings:\n\n{sb.ToString()}");
            else
                await this.InformAsync(ctx, $"Removed {count} reactions matching given IDs.", important: false);
        }

        [Command("delete"), Priority(0)]
        public async Task DeleteAsync(CommandContext ctx,
                                     [RemainingText, Description("Trigger words to remove.")] params string[] triggers)
        {
            if (triggers is null || !triggers.Any())
                throw new InvalidCommandUsageException("Triggers missing.");

            if (!this.Shared.TextReactions.TryGetValue(ctx.Guild.Id, out var treactions))
                throw new CommandFailedException("This guild has no text reactions registered.");

            var trIds = new List<int>();

            var sb = new StringBuilder();
            triggers = triggers.Select(t => t.ToLowerInvariant()).ToArray();
            foreach (string trigger in triggers)
            {
                if (string.IsNullOrWhiteSpace(trigger))
                    continue;

                if (!trigger.IsValidRegex())
                {
                    sb.AppendLine($"Error: Trigger {Formatter.Bold(trigger)} is not a valid regular expression.");
                    continue;
                }

                var found = treactions.Where(tr => tr.ContainsTriggerPattern(trigger));
                if (!found.Any())
                {
                    sb.AppendLine($"Warning: Trigger {Formatter.Bold(trigger)} does not exist in this guild.");
                    continue;
                }

                bool success = true;
                foreach (var tr in found)
                {
                    success |= tr.RemoveTrigger(trigger);
                    trIds.Add(tr.Id);
                }

                if (!success)
                {
                    sb.AppendLine($"Warning: Failed to remove some text reactions for trigger {Formatter.Bold(trigger)}.");
                    continue;
                }
            }

            using (var dc = this.Database.CreateContext())
            {
                var toUpdate = dc.TextReactions
                    .Include(t => t.DbTriggers)
                    .AsEnumerable()
                    .Where(tr => tr.GuildId == ctx.Guild.Id && trIds.Contains(tr.Id))
                    .ToList();
                foreach (DatabaseTextReaction tr in toUpdate)
                {
                    foreach (string trigger in triggers)
                        tr.DbTriggers.Remove(new DatabaseTextReactionTrigger { ReactionId = tr.Id, Trigger = trigger });
                    await dc.SaveChangesAsync();

                    if (tr.DbTriggers.Any())
                    {
                        dc.TextReactions.Remove(tr);
                        await dc.SaveChangesAsync();
                    }
                }
            }

            int count = treactions.RemoveWhere(tr => tr.RegexCount == 0);

            if (count > 0)
            {
                var logchn = this.Shared.GetLogChannelForGuild(ctx.Client, ctx.Guild);
                if (!(logchn is null))
                {
                    var emb = new DiscordEmbedBuilder
                    {
                        Title = "Several text reactions have been deleted",
                        Color = this.ModuleColor
                    };
                    emb.AddField("User responsible", ctx.User.Mention, inline: true);
                    emb.AddField("Invoked in", ctx.Channel.Mention, inline: true);
                    emb.AddField("Removed successfully", $"{count} reactions", inline: true);
                    emb.AddField("Triggers attempted to be removed", string.Join("\n", triggers));
                    if (sb.Length > 0)
                        emb.AddField("With errors", sb.ToString());
                    await logchn.SendMessageAsync(embed: emb.Build())
                        .ConfigureAwait(false);
                }
            }

            if (sb.Length > 0)
                await this.InformOfFailureAsync(ctx, $"Action finished with following warnings/errors:\n\n{sb.ToString()}");
            else
                await this.InformAsync(ctx, $"Done! {count} reactions were removed completely.", important: false);
        }

        #endregion COMMAND_TEXT_REACTIONS_DELETE

        #region COMMAND_TEXT_REACTIONS_DELETE_ALL

        [Command("deleteall"), UsageInteractivity]
        [Description("Delete all text reactions for the current guild.")]
        [Aliases("clear", "da", "c", "ca", "cl", "clearall", ">>>")]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task DeleteAllAsync(CommandContext ctx)
        {
            if (!await ctx.WaitForBoolReplyAsync("Are you sure you want to delete all text reactions for this guild?").ConfigureAwait(false))
                return;

            if (this.Shared.TextReactions.ContainsKey(ctx.Guild.Id))
                if (!this.Shared.TextReactions.TryRemove(ctx.Guild.Id, out _))
                    throw new ConcurrentOperationException("Failed to remove text reaction collection!");

            using (var dc = this.Database.CreateContext())
            {
                dc.TextReactions.RemoveRange(dc.TextReactions.Where(tr => tr.GuildId == ctx.Guild.Id));
                await dc.SaveChangesAsync();
            }

            var logchn = this.Shared.GetLogChannelForGuild(ctx.Client, ctx.Guild);
            if (!(logchn is null))
            {
                var emb = new DiscordEmbedBuilder
                {
                    Title = "All text reactions have been deleted",
                    Color = this.ModuleColor
                };
                emb.AddField("User responsible", ctx.User.Mention, inline: true);
                emb.AddField("Invoked in", ctx.Channel.Mention, inline: true);
                await logchn.SendMessageAsync(embed: emb.Build());
            }

            await this.InformAsync(ctx, "Removed all text reactions!", important: false);
        }

        #endregion COMMAND_TEXT_REACTIONS_DELETE_ALL

        #region COMMAND_TEXT_REACTIONS_FIND

        [Command("find")]
        [Description("Show a text reactions that matches the specified trigger.")]
        [Aliases("f")]
        [UsageExampleArgs("hello")]
        public Task ListAsync(CommandContext ctx,
                             [RemainingText, Description("Specific trigger.")] string trigger)
        {
            if (!this.Shared.TextReactions.TryGetValue(ctx.Guild.Id, out var treactions) || !treactions.Any())
                throw new CommandFailedException("This guild has no text reactions registered.");

            var tr = treactions.SingleOrDefault(t => t.IsMatch(trigger));
            if (tr is null)
                throw new CommandFailedException("None of the reactions respond to such trigger.");

            var emb = new DiscordEmbedBuilder
            {
                Title = "Text reaction that matches the trigger",
                Description = string.Join(" | ", tr.TriggerStrings),
                Color = this.ModuleColor
            };
            emb.AddField("ID", tr.Id.ToString(), inline: true);
            emb.AddField("Response", tr.Response, inline: true);
            return ctx.RespondAsync(embed: emb.Build());
        }

        #endregion COMMAND_TEXT_REACTIONS_FIND

        #region COMMAND_TEXT_REACTIONS_LIST

        [Command("list")]
        [Description("Show all text reactions for the guild.")]
        [Aliases("ls", "l", "print")]
        public Task ListAsync(CommandContext ctx)
        {
            if (!this.Shared.TextReactions.TryGetValue(ctx.Guild.Id, out var treactions) || !treactions.Any())
                throw new CommandFailedException("This guild has no text reactions registered.");

            return ctx.SendCollectionInPagesAsync(
                "Text reactions for this guild",
                treactions.OrderBy(tr => tr.OrderedTriggerStrings.First()),
                tr => $"{Formatter.InlineCode($"{tr.Id:D4}")} : {tr.Response} | Triggers: {string.Join(", ", tr.TriggerStrings)}",
                this.ModuleColor
            );
        }

        #endregion COMMAND_TEXT_REACTIONS_LIST

        #region HELPER_FUNCTIONS

        private async Task AddTextReactionAsync(CommandContext ctx, string trigger, string response, bool regex)
        {
            if (string.IsNullOrWhiteSpace(response))
                throw new InvalidCommandUsageException("Response missing or invalid.");

            if (trigger.Length < 2 || response.Length < 2)
                throw new CommandFailedException("Trigger or response cannot be shorter than 2 characters.");

            if (trigger.Length > 120 || response.Length > 120)
                throw new CommandFailedException("Trigger or response cannot be longer than 120 characters.");

            if (!this.Shared.TextReactions.ContainsKey(ctx.Guild.Id))
                this.Shared.TextReactions.TryAdd(ctx.Guild.Id, new ConcurrentHashSet<TextReaction>());

            if (regex && !trigger.IsValidRegex())
                throw new CommandFailedException($"Trigger {Formatter.Bold(trigger)} is not a valid regular expression.");

            if (this.Shared.GuildHasTextReaction(ctx.Guild.Id, trigger))
                throw new CommandFailedException($"Trigger {Formatter.Bold(trigger)} already exists.");

            if (this.Shared.Filters.TryGetValue(ctx.Guild.Id, out var filters) && filters.Any(f => f.Trigger.IsMatch(trigger)))
                throw new CommandFailedException($"Trigger {Formatter.Bold(trigger)} collides with an existing filter in this guild.");

            int id;
            using (var dc = this.Database.CreateContext())
            {
                var dbtr = dc.TextReactions.FirstOrDefault(tr => tr.GuildId == ctx.Guild.Id && tr.Response == response);
                if (dbtr is null)
                {
                    dbtr = new DatabaseTextReaction
                    {
                        GuildId = ctx.Guild.Id,
                        Response = response,
                    };
                    dc.TextReactions.Add(dbtr);
                    await dc.SaveChangesAsync();
                }

                dbtr.DbTriggers.Add(new DatabaseTextReactionTrigger { ReactionId = dbtr.Id, Trigger = regex ? trigger : Regex.Escape(trigger) });

                await dc.SaveChangesAsync();
                id = dbtr.Id;
            }

            var sb = new StringBuilder();

            ConcurrentHashSet<TextReaction> treactions = this.Shared.TextReactions[ctx.Guild.Id];
            var reaction = treactions.FirstOrDefault(tr => tr.Response == response);
            if (reaction is null)
            {
                if (!treactions.Add(new TextReaction(id, trigger, response, regex)))
                    throw new CommandFailedException($"Failed to add trigger {Formatter.Bold(trigger)}.");
            } else
            {
                if (!reaction.AddTrigger(trigger, regex))
                    throw new CommandFailedException($"Failed to add trigger {Formatter.Bold(trigger)}.");
            }

            var logchn = this.Shared.GetLogChannelForGuild(ctx.Client, ctx.Guild);
            if (!(logchn is null))
            {
                var emb = new DiscordEmbedBuilder
                {
                    Title = "New text reaction added",
                    Color = this.ModuleColor
                };
                emb.AddField("User responsible", ctx.User.Mention, inline: true);
                emb.AddField("Invoked in", ctx.Channel.Mention, inline: true);
                emb.AddField("Response", response, inline: true);
                emb.AddField("Trigger", trigger);
                if (sb.Length > 0)
                    emb.AddField("With errors", sb.ToString());
                await logchn.SendMessageAsync(embed: emb.Build());
            }

            if (sb.Length > 0)
                await this.InformOfFailureAsync(ctx, sb.ToString());
            else
                await this.InformAsync(ctx, "Successfully added given text reaction.", important: false);
        }

        #endregion HELPER_FUNCTIONS
    }
}

#region USING_DIRECTIVES

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
    public class ReactionsPollModule : FreudModule
    {
        public ReactionsPollModule(SharedData shared, DatabaseContextBuilder dcb)
            : base(shared, dcb)
        {
            this.ModuleColor = DiscordColor.Orange;
        }

        #region COMMAND_REACTIONS_POLL

        [Command("reactionspoll"), Priority(1)]
        [Description("Starts a pll with reactions in the channel.")]
        [Aliases("rpoll", "pollr", "voter")]
        [UsageExampleArgs(":smile: :joy:")]
        public async Task ReactionsPollAsync(CommandContext ctx,
                                            [Description("Time for poll to run.")] TimeSpan timeout,
                                            [RemainingText, Description("Question.")] string question)
        {
            if (string.IsNullOrWhiteSpace(question))
                throw new InvalidCommandUsageException("Poll requires a question.");

            if (PollService.IsPollRunningInChannel(ctx.Channel.Id))
                throw new CommandFailedException("Another poll is already running in this channel...");

            if (timeout < TimeSpan.FromSeconds(10) || timeout >= TimeSpan.FromDays(1))
                throw new InvalidCommandUsageException("Poll cannot run for less than 10 seconds or more than 1 day(s).");

            var rpoll = new ReactionsPoll(ctx.Client.GetInteractivity(), ctx.Channel, ctx.Member, question);
            PollService.RegisterPollInChannel(rpoll, ctx.Channel.Id);
            try
            {
                await this.InformAsync(ctx, StaticDiscordEmoji.Question, "And what will be the possible answers? (separate with a semicolon)");
                var options = await ctx.WaitAndParsePollOptionsAsync();
                if (options.Count < 2 || options.Count > 10)
                    throw new CommandFailedException("Poll must have minimum 2 and maximum 10 options!");
                rpoll.Options = options;

                await rpoll.RunAsync(timeout);
            } finally
            {
                PollService.UnregisterPollInChannel(ctx.Channel.Id);
            }
        }

        [Command("reactionspoll"), Priority(1)]
        public Task ReactionsPollAsync(CommandContext ctx, [RemainingText, Description("Question.")] string question)
            => this.ReactionsPollAsync(ctx, TimeSpan.FromMinutes(1), question);

        #endregion COMMAND_REACTIONS_POLL
    }
}

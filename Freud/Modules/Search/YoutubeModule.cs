#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using Freud.Common.Attributes;
using Freud.Database.Db;
using Freud.Exceptions;
using Freud.Modules.Search.Services;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Search
{
    [Group("youtube"), Module(ModuleType.Searches), NotBlocked]
    [Description("Youtube search commands. Group call searches YouTube for a given query.")]
    [Aliases("y", "yt", "ytube")]
    [UsageExampleArgs("never gonna give you up")]
    [Cooldown(3, 5, CooldownBucketType.Channel)]
    public class YoutubeModule : FreudServiceModule<YtService>
    {
        public YoutubeModule(YtService yt, SharedData shared, DatabaseContextBuilder dcb)
            : base(yt, shared, dcb)
        {
            this.ModuleColor = DiscordColor.Red;
        }

        [GroupCommand]
        public Task ExecuteGroupAsync(CommandContext ctx, [RemainingText, Description("Search query.")] string query)
            => this.SearchAndSendResultsAsync(ctx, 5, query);

        #region COMMAND_YOUTUBE_SEARCH

        [Command("search")]
        [Description("Youtube searcher")]
        [Aliases("s")]
        [UsageExampleArgs("Katy Perry")]
        public Task AdvancedSearchAsync(CommandContext ctx,
                                       [Description("Amount of results. [1-20]")] int amount,
                                       [RemainingText, Description("Search query")] string query)
            => this.SearchAndSendResultsAsync(ctx, amount, query);

        #endregion COMMAND_YOUTUBE_SEARCH

        #region COMMAND_YOUTUBE_SEARCH_VIDEO

        [Command("searchvideo")]
        [Description("Youtube searcher for videos only.")]
        [Aliases("sv", "searchv", "video")]
        [UsageExampleArgs("Katy Perry Dark Horse")]
        public Task SearchVideoAsync(CommandContext ctx,
                                    [RemainingText, Description("Search query")] string query)
            => this.SearchAndSendResultsAsync(ctx, 5, query, "video");

        #endregion COMMAND_YOUTUBE_SEARCH_VIDEO

        #region COMMAND_YOUTUBE_SEARCH_CHANNEL

        [Command("searchchannel")]
        [Description("Youtube searcher for channls only.")]
        [Aliases("sc", "searchc", "channel")]
        [UsageExampleArgs("PewDiePie")]
        public Task SearchChannelAsync(CommandContext ctx,
                                      [RemainingText, Description("Search query")] string query)
            => this.SearchAndSendResultsAsync(ctx, 5, query, "channel");

        #endregion COMMAND_YOUTUBE_SEARCH_CHANNEL

        #region COMMAND_YOUTUBE_SEARCH_PLAYLIST

        [Command("searchp")]
        [Description("Youtube searcher for playlist only.")]
        [Aliases("sp", "searchplaylist", "playlist")]
        [UsageExampleArgs("Kpop")]
        public Task SearchPlaylistAsync(CommandContext ctx,
                                       [RemainingText, Description("Search query")] string query)
            => this.SearchAndSendResultsAsync(ctx, 5, query, "playlist");

        #endregion COMMAND_YOUTUBE_SEARCH_PLAYLIST

        #region COMMAND_YOUTUBE_SUBSCRIBE

        [Command("subscribe")]
        [Description("Add a new subscription for a YouTube channel.")]
        [Aliases("add", "a", "+", "sub")]
        [UsageExampleArgs("https://www.youtube.com/user/PewDiePie", "PewDiePie")]
        [RequireUserPermissions(Permissions.ManageGuild)]
        public Task SubscribeAsync(CommandContext ctx,
                                  [Description("Channel URL")] string url,
                                  [Description("Friendly name")] string name = null)
        {
            string command = $"sub yt {url} {name}";
            var cmd = ctx.CommandsNext.FindCommand(command, out string args);
            var fctx = ctx.CommandsNext.CreateFakeContext(ctx.Member, ctx.Channel, command, ctx.Prefix, cmd, args);

            return ctx.CommandsNext.ExecuteCommandAsync(fctx);
        }

        #endregion COMMAND_YOUTUBE_SUBSCRIBE

        #region COMMAND_YOUTUBE_UNSUBSCRIBE

        [Command("unsubscribe")]
        [Description("Remove a YouTube channel subscription.")]
        [Aliases("del", "d", "rm", "-", "unsub")]
        [UsageExampleArgs("https://www.youtube.com/user/PewDiePie", "PewDiePie")]
        [RequireUserPermissions(Permissions.ManageGuild)]
        public Task UnsubscribeAsync(CommandContext ctx,
                                    [Description("Channel URL or subscription name")] string name_url)
        {
            string command = $"unsub yt {name_url}";
            var cmd = ctx.CommandsNext.FindCommand(command, out string args);
            var fctx = ctx.CommandsNext.CreateFakeContext(ctx.Member, ctx.Channel, command, ctx.Prefix, cmd, args);

            return ctx.CommandsNext.ExecuteCommandAsync(fctx);
        }

        #endregion COMMAND_YOUTUBE_UNSUBSCRIBE

        #region HELPER_FUNCTIONS

        private async Task SearchAndSendResultsAsync(CommandContext ctx, int amount, string query, string type = null)
        {
            if (this.Service.IsDisabled())
                throw new ServiceDisabledException();

            if (string.IsNullOrWhiteSpace(query))
                throw new InvalidCommandUsageException("Search query missing");

            var pages = await this.Service.GetPaginatedResultsAsync(query, amount, type);
            if (pages is null)
            {
                await this.InformOfFailureAsync(ctx, "No results found!");
                return;
            }

            await ctx.Client.GetInteractivity().SendPaginatedMessageAsync(ctx.Channel, ctx.User, pages);
        }

        #endregion HELPER_FUNCTIONS
    }
}

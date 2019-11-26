#region USING_DIRECTIVES

using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using Freud.Common.Attributes;
using Freud.Database.Db;
using Freud.Exceptions;
using Freud.Modules.Search.Common;
using Freud.Modules.Search.Extensions;
using Freud.Modules.Search.Services;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Search
{
    [Group("imdb"), Module(ModuleType.Searches), NotBlocked]
    [Description("Search Open Movie Database. Group call searches by title.")]
    [Aliases("movies", "series", "serie", "movie", "film", "cinema", "omdb")]
    [UsageExampleArgs("Airplane")]
    [Cooldown(3, 5, CooldownBucketType.Channel)]
    public class OMDbModule : FreudServiceModule<OMDbService>
    {
        public OMDbModule(OMDbService omdb, SharedData shared, DatabaseContextBuilder db)
            : base(omdb, shared, db)
        {
            this.ModuleColor = DiscordColor.Yellow;
        }

        [GroupCommand, Priority(0)]
        public Task ExecuteGroupAsync(CommandContext ctx,
                                     [RemainingText, Description("Title.")] string title)
            => this.SearchByTitleAsync(ctx, title);

        #region COMMAND_IMDB_SEARCH

        [Command("search")]
        [Description("Searches IMDb for given query and returns paginated results.")]
        [Aliases("s", "find")]
        [UsageExampleArgs("Sharknado")]
        public async Task SearchAsync(CommandContext ctx,
                                     [RemainingText, Description("Search query.")] string query)
        {
            if (this.Service.IsDisabled())
                throw new ServiceDisabledException();

            var pages = await this.Service.GetPaginatedResultsAsync(query);
            if (pages is null)
            {
                await this.InformOfFailureAsync(ctx, "No results found!");
                return;
            }

            await ctx.Client.GetInteractivity().SendPaginatedMessageAsync(ctx.Channel, ctx.User, pages);
        }

        #endregion COMMAND_IMDB_SEARCH

        #region COMMAND_IMDB_TITLE

        [Command("title")]
        [Description("Search by title.")]
        [Aliases("t", "name", "n")]
        [UsageExampleArgs("Airplane")]
        public Task SearchByTitleAsync(CommandContext ctx,
                                      [RemainingText, Description("Title.")] string title)
            => this.SearchAndSendResultAsync(ctx, OMDbQueryType.Title, title);

        #endregion COMMAND_IMDB_TITLE

        #region COMMAND_IMDB_ID

        [Command("id")]
        [Description("Search by IMDb ID.")]
        [UsageExampleArgs("tt4158110")]
        public Task SearchByIdAsync(CommandContext ctx,
                                   [Description("ID.")] string id)
            => this.SearchAndSendResultAsync(ctx, OMDbQueryType.Id, id);

        #endregion COMMAND_IMDB_ID

        #region HELPER_FUNCTIONS

        private async Task SearchAndSendResultAsync(CommandContext ctx, OMDbQueryType type, string query)
        {
            if (this.Service.IsDisabled())
                throw new ServiceDisabledException();

            var info = await this.Service.GetSingleResultAsync(type, query);
            if (info is null)
            {
                await this.InformOfFailureAsync(ctx, "No results found!");
                return;
            }

            await ctx.RespondAsync(embed: info.ToDiscordEmbed(this.ModuleColor));
        }

        #endregion HELPER_FUNCTIONS
    }
}

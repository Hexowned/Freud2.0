#region USING_DIRECTIVES

using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using Freud.Common.Attributes;
using Freud.Database.Db;
using Freud.Modules.Search.Services;
using System.Linq;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Search
{
    [Group("wikipedia"), Module(ModuleType.Searches), NotBlocked]
    [Description("Wikipedia search. If invoked without a subcommand, searches Wikipedia with given query.")]
    [Aliases("wiki")]
    [UsageExampleArgs("Linux")]
    [Cooldown(3, 5, CooldownBucketType.Channel)]
    public class WikiModule : FreudModule
    {
        public WikiModule(SharedData shared, DatabaseContextBuilder db)
           : base(shared, db)
        {
            this.ModuleColor = DiscordColor.White;
        }

        [GroupCommand]
        public Task ExecuteGroupAsync(CommandContext ctx,
                                     [RemainingText, Description("Query.")] string query)
            => this.SearchAsync(ctx, query);

        #region COMMAND_WIKI_SEARCH

        [Command("search")]
        [Description("Search Wikipedia for a given query.")]
        [Aliases("s", "find")]
        [UsageExampleArgs("Linux")]
        public async Task SearchAsync(CommandContext ctx,
                                    [RemainingText, Description("Query.")] string query)
        {
            var res = await WikiService.SearchAsync(query);
            if (res is null || !res.Any())
            {
                await this.InformOfFailureAsync(ctx, "No results...");
                return;
            }

            await ctx.Client.GetInteractivity().SendPaginatedMessageAsync(ctx.Channel, ctx.User, res.Select(r
                => new Page(embed: new DiscordEmbedBuilder
                {
                    Title = r.Title,
                    Description = string.IsNullOrWhiteSpace(r.Snippet) ? "No description provided" : r.Snippet,
                    Url = r.Url,
                    Color = this.ModuleColor
                }.WithFooter("Powered by Wikipedia API", WikiService.WikipediaIconUrl))));
        }

        #endregion COMMAND_WIKI_SEARCH
    }
}

#region USING_DIRECTIVES

using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Freud.Common.Attributes;
using Freud.Database.Db;
using Freud.Exceptions;
using Freud.Modules.Search.Services;
using GiphyDotNet.Model.GiphyImage;
using System.Linq;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Search
{
    [Group("gif"), Module(ModuleType.Searches), NotBlocked]
    [Description("GIPHY commands. If invoked without a subcommand, searches GIPHY with given query.")]
    [Aliases("giphy")]
    [UsageExampleArgs("wat")]
    [Cooldown(3, 5, CooldownBucketType.Channel)]
    public class GiphyModule : FreudServiceModule<GiphyService>
    {
        public GiphyModule(GiphyService giphy, SharedData shared, DatabaseContextBuilder dcb)
            : base(giphy, shared, dcb)
        {
            this.ModuleColor = DiscordColor.Violet;
        }

        [GroupCommand]
        public async Task ExecuteGroupAsync(CommandContext ctx, [RemainingText, Description("Query.")] string query)
        {
            if (this.Service.IsDisabled())
                throw new ServiceDisabledException();

            Data[] res = await this.Service.SearchAsync(query);
            if (!res.Any())
            {
                await this.InformOfFailureAsync(ctx, "No results...");
                return;
            }

            await ctx.RespondAsync(res.First().Url);
        }

        #region COMMAND_GIPHY_RANDOM

        [Command("random")]
        [Description("Return a random GIF.")]
        [Aliases("r", "rand", "rnd")]
        public async Task RandomAsync(CommandContext ctx)
        {
            if (this.Service.IsDisabled())
                throw new ServiceDisabledException();
            // RandomImage
            var res = await this.Service.GetRandomGifAsync();

            await ctx.RespondAsync(embed: new DiscordEmbedBuilder
            {
                Title = "Random gif:",
                ImageUrl = res.Url,
                Color = this.ModuleColor
            }.Build());
        }

        #endregion COMMAND_GIPHY_RANDOM

        #region COMMAND_GIPHY_TRENDING

        [Command("trending")]
        [Description("Return an amount of trending GIFs.")]
        [Aliases("t", "tr", "trend")]
        [UsageExampleArgs("3")]
        public async Task TrendingAsync(CommandContext ctx, [Description("Number of results (1-10)")] int amount = 5)
        {
            if (this.Service.IsDisabled())
                throw new ServiceDisabledException();

            Data[] res = await this.Service.GetTrendingGifsAsync(amount);

            var emb = new DiscordEmbedBuilder
            {
                Title = "Trending gifs:",
                Color = this.ModuleColor
            };
            // gif = data
            foreach (var gif in res)
                emb.AddField($"{gif.Username} (rating: {gif.Rating})", gif.EmbedUrl);

            await ctx.RespondAsync(embed: emb.Build());
        }

        #endregion COMMAND_GIPHY_TRENDING
    }
}

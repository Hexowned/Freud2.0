#region USING_DIRECTIVES

using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Freud.Common.Attributes;
using Freud.Database.Db;
using Freud.Exceptions;
using Freud.Modules.Search.Extensions;
using Freud.Modules.Search.Services;
using System.Net;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Search
{
    [Module(ModuleType.Searches), NotBlocked]
    [Cooldown(5, 10, CooldownBucketType.Channel)]
    public class SearchModule : FreudModule
    {
        public SearchModule(SharedData shared, DatabaseContextBuilder dcb)
            : base(shared, dcb)
        {
            this.ModuleColor = DiscordColor.MidnightBlue;
        }

        #region SEARCH_COMMAND_CAT

        [Command("cat")]
        [Description("Get a random cat image!")]
        [Aliases("kitty", "kitten")]
        public async Task RandomCatAsync(CommandContext ctx)
        {
            string url = await PetImagesService.GetRandomCatImageAsync();
            if (url is null)
                throw new CommandFailedException("Connection a random.cat failed!");

            await ctx.RespondAsync(embed: new DiscordEmbedBuilder
            {
                Description = DiscordEmoji.FromName(ctx.Client, ":cat:"),
                ImageUrl = url,
                Color = this.ModuleColor
            });
        }

        #endregion SEARCH_COMMAND_CAT

        #region COMMAND_SEARCH_DOG

        [Command("dog")]
        [Description("Get a random dog image!")]
        [Aliases("doge", "puppy", "pup", "doggy")]
        public async Task RandomDogAsync(CommandContext ctx)
        {
            string url = await PetImagesService.GetRandomDogImageAsync();
            if (url is null)
                throw new CommandFailedException("Connection to random.dog failed!");

            await ctx.RespondAsync(embed: new DiscordEmbedBuilder
            {
                Description = DiscordEmoji.FromName(ctx.Client, ":dog:"),
                ImageUrl = url,
                Color = this.ModuleColor
            });
        }

        #endregion COMMAND_SEARCH_DOG

        #region COMMAND_SEARCH_IP_STACK

        [Command("ipstack")]
        [Description("Retrieve IP geolocation information.")]
        [Aliases("ip", "geolocation", "iplocation", "iptracker", "iptrack", "trackip", "iplocate", "geoip")]
        [UsageExampleArgs("123.123.123.123")]
        public async Task ExecuteGroupAsync(CommandContext ctx, [Description("IP.")] IPAddress ip)
        {
            var info = await IpGeolocationService.GetInfoForIpAsync(ip);

            if (!info.Success)
                throw new CommandFailedException($"Retrieving IP geolocation info failed! Details: {info.ErrorMessage}");

            await ctx.RespondAsync(embed: info.ToDiscordEmbed(this.ModuleColor));
        }

        #endregion COMMAND_SEARCH_IP_STACK

        #region COMMAND_SEARCH_NEWS

        [Command("news")]
        [Description("Get newest world news.")]
        [Aliases("worldnews")]
        public Task NewsRssAsync(CommandContext ctx)
        {
            var res = RssService.GetFeedResults("");
            if (res is null)
                throw new CommandFailedException("Error getting world news.");

            return RssService.SendFeedResultsAsync(ctx.Channel, res);
        }

        #endregion COMMAND_SEARCH_NEWS

        #region COMMAND_SEARCH_QUOTE_OF_THE_DAY

        [Command("quoteoftheday")]
        [Description("Get quote of the day. You can also specify a category from the list: inspire, management, sports, life, funny, love, art, students.")]
        [Aliases("qotd", "qod", "quote", "q")]
        [UsageExampleArgs("life")]
        public async Task QuoteOfTheDayAsync(CommandContext ctx, [Description("Category.")] string category = null)
        {
            var quote = await QuoteService.GetQuoteOfTheDayAsync(category);
            if (quote is null)
                throw new CommandFailedException("Failed to retrieve quote! Possibly the given quote category does not exsits.");

            await ctx.RespondAsync(embed: quote.ToDiscordEmbed($"Quote of the day{(string.IsNullOrWhiteSpace(category) ? "" : $" in category {category}")}"));
        }

        #endregion COMMAND_SEARCH_QUOTE_OF_THE_DAY
    }
}

#region USING_DIRECTIVES

using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using Freud.Common.Attributes;
using Freud.Database.Db;
using Freud.Exceptions;
using Freud.Modules.Search.Services;
using System.Linq;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Search
{
    [Group("weather"), Module(ModuleType.Searches), NotBlocked]
    [Description("Weather search commands. Group call returns weather information for given query.")]
    [Aliases("w")]
    [UsageExampleArgs("london")]
    [Cooldown(3, 5, CooldownBucketType.Channel)]
    public class WeatherModule : FreudServiceModule<WeatherService>
    {
        public WeatherModule(WeatherService weather, SharedData shared, DatabaseContextBuilder db)
            : base(weather, shared, db)
        {
            this.ModuleColor = DiscordColor.Aquamarine;
        }

        [GroupCommand]
        public async Task ExecuteGroupAsync(CommandContext ctx,
                                           [RemainingText, Description("Query.")] string query)
        {
            if (this.Service.IsDisabled())
                throw new ServiceDisabledException();

            if (string.IsNullOrWhiteSpace(query))
                throw new InvalidCommandUsageException("You need to specify a query (city usually).");

            var em = await this.Service.GetEmbeddedCurrentWeatherDataAsync(query);
            if (em is null)
                throw new CommandFailedException("Cannot find weather data for given query.");

            await ctx.RespondAsync(embed: em);
        }

        #region COMMAND_WEATHER_FORECAST

        [Command("forecast"), Priority(1)]
        [Description("Get weather forecast for the following days (def: 7).")]
        [Aliases("f")]
        [UsageExampleArgs("london", "5 london")]
        public async Task ForecastAsync(CommandContext ctx,
                                       [Description("Amount of days to fetch the forecast for.")] int amount,
                                       [RemainingText, Description("Query.")] string query)
        {
            if (this.Service.IsDisabled())
                throw new ServiceDisabledException();

            if (string.IsNullOrWhiteSpace(query))
                throw new InvalidCommandUsageException("You need to specify a query (city usually).");

            var ems = await this.Service.GetEmbeddedWeatherForecastAsync(query, amount);
            if (ems is null || !ems.Any())
                throw new CommandFailedException("Cannot find weather data for given query.");

            await ctx.Client.GetInteractivity().SendPaginatedMessageAsync(ctx.Channel, ctx.User, ems.Select(e => new Page(embed: e)));
        }

        [Command("forecast"), Priority(0)]
        public Task ForecastAsync(CommandContext ctx,
                                 [RemainingText, Description("Query.")] string query)
            => this.ForecastAsync(ctx, 7, query);

        #endregion COMMAND_WEATHER_FORECAST
    }
}

#region USING_DIRECTIVES

using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Freud.Common.Attributes;
using Freud.Database.Db;
using Freud.Extensions.Discord;
using Freud.Modules.Search.Services;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Search
{
    [Group("urbandict"), Module(ModuleType.Searches), NotBlocked]
    [Description("Urban Dictionary commands. Group call searches Urban Dictionary for a given query.")]
    [Aliases("ud", "urban")]
    [UsageExampleArgs("blonde")]
    [Cooldown(3, 5, CooldownBucketType.Channel)]
    public class UrbanDictionaryModule : FreudModule
    {
        public UrbanDictionaryModule(SharedData shared, DatabaseContextBuilder dcb)
            : base(shared, dcb)
        {
            this.ModuleColor = DiscordColor.CornflowerBlue;
        }

        [GroupCommand]
        public async Task ExecuteGroupAsync(CommandContext ctx, [RemainingText, Description("Query.")] string query)
        {
            var data = await UrbanDictionaryService.GetDefinitionForTermAsync(query);

            if (data is null)
            {
                await this.InformOfFailureAsync(ctx, "No results found!");
                return;
            }

            await ctx.SendCollectionInPagesAsync($"Urban Dictionary search results for \"{query}\"",
                                                 data.List, res => res.ToInfoString(), this.ModuleColor, 1);
        }
    }
}

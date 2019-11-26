#region USING_DIRECTIVES

using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using Freud.Common.Attributes;
using Freud.Database.Db;
using Freud.Exceptions;
using Freud.Modules.Search.Extensions;
using Freud.Modules.Search.Services;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Search
{
    [Group("goodreads"), Module(ModuleType.Searches), NotBlocked]
    [Description("Goodreads commands. Group call searches Goodreads books with given query.")]
    [Aliases("gr")]
    [UsageExampleArgs("Ender's Game")]
    [Cooldown(3, 5, CooldownBucketType.Channel)]
    public class GoodreadsModule : FreudServiceModule<GoodreadsService>
    {
        public GoodreadsModule(GoodreadsService goodreads, SharedData shared, DatabaseContextBuilder dcb)
            : base(goodreads, shared, dcb)
        {
            this.ModuleColor = DiscordColor.DarkGray;
        }

        [GroupCommand]
        public Task ExecuteGroupAsync(CommandContext ctx, [RemainingText, Description("Query.")] string query)
            => this.SearchBookAsync(ctx, query);

        #region COMMAND_GOODREADS

        [Command("book")]
        [Description("Search Goodreads books by title, author, or ISBN.")]
        [Aliases("books", "b")]
        [UsageExampleArgs("Ender's Game")]
        public async Task SearchBookAsync(CommandContext ctx,
                                         [RemainingText, Description("Query.")] string query)
        {
            if (this.Service.IsDisabled())
                throw new ServiceDisabledException();

            var res = await this.Service.SearchBooksAsync(query);
            await ctx.Client.GetInteractivity().SendPaginatedMessageAsync(ctx.Channel, ctx.User, res.ToDiscordPages());
        }

        #endregion COMMAND_GOODREADS
    }
}

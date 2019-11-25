#region USING_DIRECTIVES

using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Freud.Database.Db;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Reactions
{
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

        #region HELPER_FUNCTIONS
    }
}

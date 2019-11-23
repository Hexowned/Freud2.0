#region USING_DIRECTIVES

using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Common.Attributes
{
    public sealed class UsageInteractivityAttribute : CheckBaseAttribute
    {
        public override Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
            if (ctx.Services.GetService<SharedData>().PendingResponseExists(ctx.Channel.Id, ctx.User.Id))
                return Task.FromResult(false);
            else
                return Task.FromResult(true);
        }
    }
}

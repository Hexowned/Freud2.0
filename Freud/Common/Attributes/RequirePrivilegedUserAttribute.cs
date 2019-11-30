#region USING_DIRECTIVES

using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using Freud.Database.Db;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class RequirePrivilegedUserAttribute : CheckBaseAttribute
    {
        public override Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
            if (ctx.User.Id == ctx.Client.CurrentApplication.Team.Id)
                return Task.FromResult(true);

            using (var dc = ctx.Services.GetService<DatabaseContextBuilder>().CreateContext())
                return Task.FromResult(dc.PrivilegedUsers.Any(u => u.UserId == ctx.User.Id));
        }
    }
}

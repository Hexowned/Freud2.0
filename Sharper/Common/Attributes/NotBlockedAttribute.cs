#region USING_DIRECTIVES
using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Sharper.Database;
using Sharper.Database.Entities;
#endregion
namespace Sharper.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class NotBlockedAttribute : CheckBaseAttribute
    {
        public override Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
            SharedData shared = ctx.Services.GetService<SharedData>();
            if (shared.ListeningStatus)
            {
                if (shared.BlockedUsers.Contains(ctx.User.Id) || shared.BlockedChannels.Contains(ctx.Channel.Id))
                    return Task.FromResult(false);

                if (this.BlockingCommandRuleExists(ctx))
                    return Task.FromResult(false);

                if (!help)
                {
                    ctx.Client.DebugLogger.LogMessage(LogLevel.Debug, Sharper.ApplicationName,
                        $"Executing: {ctx.Command?.QualifiedName ?? "<unknown command>"}\n" +
                        $"{ctx.User.ToString()}\n" +
                        $"{ctx.Guild.ToString()} ; {ctx.Channel.ToString()}\n" +
                        $"Full message: {ctx.Message.Content}",
                        DateTime.Now);
                }

                return Task.FromResult(true);
            } else
            {
                return Task.FromResult(false);
            }
        }

        private bool BlockingCommandRuleExists(CommandContext ctx)
        {
            DatabaseContextBuilder dbb = ctx.Services.GetService<DatabaseContextBuilder>();
            using (DatabaseContext db = dbb.CreateContext())
            {
                IQueryable<DatabaseCommandRule> dbrules = db.commandRules
                    .Where(cr => cr.GuildId == ctx.Guild.Id && (cr.ChannelId == ctx.Channel.Id || cr.ChannelId == 0) && ctx.Command.QualifiedName.StartsWith(cr.Command));
                if (!dbrules.Any() || dbrules.Any(cr => cr.ChannelId == ctx.Channel.Id && cr.Allowed))
                    return false;
            }

            return true;
        }
    }
}

#region USING_DIRECTIVES

using DSharpPlus.Entities;
using Freud.Modules.Administration;
using System.Linq;

#endregion USING_DIRECTIVES

namespace Freud.EventListeners.Extensions
{
    public static class DSharpLoggingExtension
    {
        public static bool IsExempted(this DiscordChannel channel, FreudShard shard)
        {
            using (var dc = shard.Database.CreateContext())
            {
                if (dc.LoggingExempts.Any(ee => ee.GuildId == channel.GuildId && ee.Type == ExemptedEntityType.Channel && (ee.Id == channel.Id || ee.Id == channel.Parent.Id)))
                    return true;
            }

            return false;
        }

        public static bool IsExempted(this DiscordMember member, FreudShard shard)
        {
            if (member is null)
                return false;

            using (var dc = shard.Database.CreateContext())
            {
                if (dc.LoggingExempts.Any(ee => ee.Type == ExemptedEntityType.Member && ee.Id == member.Id))
                    return true;
                if (member.Roles.Any(r => dc.LoggingExempts.Any(ee => ee.Type == ExemptedEntityType.Role && ee.Id == r.Id)))
                    return true;
            }

            return false;
        }
    }
}

#region USING_DIRECTIVES

using DSharpPlus.Entities;
using Freud.Common.Configuration;
using Freud.Database.Db;
using System;
using System.Linq;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Discord.Extensions
{
    internal static class DiscordGuildExtensions
    {
        public static async Task<DiscordAuditLogEntry> GetLatestAuditLogEntryAsync(this DiscordGuild guild, AuditLogActionType type)
        {
            try
            {
                var entry = (await guild.GetAuditLogsAsync(1, action_type: type))?.FirstOrDefault();

                if (entry is null || DateTime.UtcNow - entry.CreationTimestamp.ToUniversalTime() > TimeSpan.FromSeconds(5))
                    return null;

                return entry;
            } catch
            {
                // swallow
            }

            return null;
        }

        public static DatabaseGuildConfiguration GetGuildConfiguration(this DiscordGuild guild, DatabaseContext dc)
            => dc.GuildConfiguration.SingleOrDefault(cfg => cfg.GuildId == guild.Id);

        public static DatabaseGuildConfiguration GetGuildSettings(this DiscordGuild guild, DatabaseContextBuilder dcb)
        {
            DatabaseGuildConfiguration gcfg = null;
            using (var dc = dcb.CreateContext())
                gcfg = guild.GetGuildConfiguration(dc);

            return gcfg;
        }
    }
}

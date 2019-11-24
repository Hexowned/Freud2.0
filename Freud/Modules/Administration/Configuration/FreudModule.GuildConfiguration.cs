#region USING_DIRECTIVES

using Freud.Common.Configuration;
using Freud.Database.Db;
using System;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Administration.Configuration
{
    public static class FreudModuleGuildConfigExtensions
    {
        public static async Task<DatabaseGuildConfiguration> GetGuildConfigurationAsync(this FreudModule module, ulong gid)
        {
            DatabaseGuildConfiguration gcfg = null;
            using (DatabaseContext dc = module.Database.CreateContext())
                gcfg = await dc.GuildConfiguration.FindAsync((long)gid) ?? new DatabaseGuildConfiguration();

            return gcfg;
        }

        public static async Task<DatabaseGuildConfiguration> ModifyGuildConfigAsync(this FreudModule module, ulong gid, Action<DatabaseGuildConfig> action)
        {
            DatabaseGuildConfiguration gcfg = null;
            using (DatabaseContext dc = module.Database.CreateContext())
            {
                gcfg = await dc.GuildConfig.FindAsync((long)gid) ?? new DatabaseGuildConfiguration();
                action(gcfg);
                dc.GuildConfig.Update(gcfg);
                await dc.SaveChangesAsync();
            }

            CachedGuildConfig cgcfg = module.Shared.GetGuildConfig(gid);
            cgcfg = gcfg.CachedConfig;
            module.Shared.UpdateGuildConfig(gid, _ => cgcfg);

            return gcfg;
        }
    }
}

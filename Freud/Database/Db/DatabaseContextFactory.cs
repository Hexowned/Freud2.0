#region USING_DIRECTIVES

using Freud.Common.Configuration;
using Microsoft.EntityFrameworkCore.Design;
using Newtonsoft.Json;
using System.IO;
using System.Text;

#endregion USING_DIRECTIVES

namespace Freud.Database.Db
{
    public class DatabaseContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext(params string[] args)
        {
            var cfg = BotConfiguration.Default;
            string json = "{}";
            var utf8 = new UTF8Encoding(false);
            var fi = new FileInfo("Frued.Resources/configuration.json");

            if (fi.Exists)
            {
                try
                {
                    using (var fs = fi.OpenRead())
                    using (var sr = new StreamReader(fs, utf8))
                        json = sr.ReadToEnd();
                    cfg = JsonConvert.DeserializeObject<BotConfiguration>(json);
                } catch
                {
                    cfg = BotConfiguration.Default;
                }
            }

            return new DatabaseContextBuilder(cfg.DatabaseConfiguration).CreateContext();
        }
    }
}

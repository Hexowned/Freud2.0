#region USING_DIRECTIVES

using Microsoft.EntityFrameworkCore.Design;
using Newtonsoft.Json;
using Sharper.Common.Configuration;
using System.IO;
using System.Text;

#endregion USING_DIRECTIVES

namespace Sharper.Database
{
    public class DatabaseContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext(params string[] args)
        {
            BotConfiguration cfg = BotConfiguration.Default;
            string json = "{}";
            var utf8 = new UTF8Encoding(false);
            var fi = new FileInfo("Resources/config.json");
            if (fi.Exists)
            {
                try
                {
                    using (FileStream fs = fi.OpenRead())
                    using (var sr = new StreamReader(fs, utf8))
                        json = sr.ReadToEnd();
                    cfg = JsonConvert.DeserializeObject<BotConfiguration>(json);
                }
                catch
                {
                    cfg = BotConfiguration.Default;
                }
            }

            return new DatabaseContextBuilder(cfg.DatabaseConfiguration).CreateContext();
        }
    }
}
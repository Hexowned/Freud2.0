#region USING_DIRECTIVES

using Microsoft.EntityFrameworkCore;
using System;
using static Freud.Database.Db.DatabaseConfiguration;

#endregion USING_DIRECTIVES

namespace Freud.Database.Db
{
    public class DatabaseContext : DbContext
    {
        // DBSet fields

        private string ConnectionString { get; }
        private DatabaseProvider Provider { get; }

        public DatabaseContext(DatabaseProvider provider, string connectionString)
        {
            this.Provider = provider;
            this.ConnectionString = connectionString;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
                return;

            optionsBuilder.ConfigureWarnings(warnings => warnings.Throw(CoreEventId.IncludeIgnoredWarning));

            switch (this.Provider)
            {
                case DatabaseProvider.PostgreSQL:
                    optionsBuilder.UseNpgsql(this.ConnectionString);
                    break;

                case DatabaseProvider.SQLite:
                    optionsBuilder.UseSqlite(this.ConnectionString);
                    break;

                case DatabaseProvider.SQLServer:
                    optionsBuilder.UseSqlServer(this.ConnectionString);
                    break;

                default:
                    throw new NotSupportedException("Provider not supported!");
            }
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.HasDefaultSchema("Freud");

            // entities
        }
    }
}

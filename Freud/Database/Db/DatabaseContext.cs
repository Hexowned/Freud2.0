#region USING_DIRECTIVES

using Freud.Database.Db.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using static Freud.Database.Db.DatabaseConfiguration;

#endregion USING_DIRECTIVES

namespace Freud.Database.Db
{
    public class DatabaseContext : DbContext
    {
        public virtual DbSet<DatabaseExemptAntispam> AntispamExempts { get; set; }
        public virtual DbSet<DatabaseAutoRole> AutoAssignableRoles { get; set; }
        public virtual DbSet<DatabaseBankAccount> BankAccounts { get; set; }
        public virtual DbSet<DatabaseBirthday> Birthdays { get; set; }
        public virtual DbSet<DatabaseBlockedChannel> BlockedChannels { get; set; }
        public virtual DbSet<DatabaseBlockedUser> BlockedUsers { get; set; }
        public virtual DbSet<DatabaseBotStatus> BotStatuses { get; set; }
        public virtual DbSet<DatabaseChicken> Chickens { get; set; }
        public virtual DbSet<DatabaseChickenBoughtUpgrade> ChickensBoughtUpgrades { get; set; }
        public virtual DbSet<DatabaseChickenUpgrade> ChickenUpgrades { get; set; }
        public virtual DbSet<DatabaseCommandRule> CommandRules { get; set; }
        public virtual DbSet<DatabaseEmojiReaction> EmojiReactions { get; set; }
        public virtual DbSet<DatabaseFilter> Filters { get; set; }
        public virtual DbSet<DatabaseForbiddenName> ForbiddenNames { get; set; }
        public virtual DbSet<DatabaseGameStats> GameStats { get; set; }
        public virtual DbSet<DatabaseGuildConfig> GuildConfig { get; set; }
        public virtual DbSet<DatabaseGuildRank> GuildRanks { get; set; }
        public virtual DbSet<DatabaseInsult> Insults { get; set; }
        public virtual DbSet<DatabaseExemptLogging> LoggingExempts { get; set; }
        public virtual DbSet<DatabaseMeme> Memes { get; set; }
        public virtual DbSet<DatabaseMessageCount> MessageCount { get; set; }
        public virtual DbSet<DatabasePrivilegedUser> PrivilegedUsers { get; set; }
        public virtual DbSet<DatabasePurchasableItem> PurchasableItems { get; set; }
        public virtual DbSet<DatabasePurchasedItem> PurchasedItems { get; set; }
        public virtual DbSet<DatabaseExemptRatelimit> RatelimitExempts { get; set; }
        public virtual DbSet<DatabaseReminder> Reminders { get; set; }
        public virtual DbSet<DatabaseRssFeed> RssFeeds { get; set; }
        public virtual DbSet<DatabaseRssSubscription> RssSubscriptions { get; set; }
        public virtual DbSet<DatabaseSavedTask> SavedTasks { get; set; }
        public virtual DbSet<DatabaseSelfRole> SelfAssignableRoles { get; set; }
        public virtual DbSet<DatabaseSwatPlayer> SwatPlayers { get; set; }
        public virtual DbSet<DatabaseSwatServer> SwatServers { get; set; }
        public virtual DbSet<DatabaseTextReaction> TextReactions { get; set; }

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
            model.HasDefaultSchema("f");

            // TODO:
        }
    }
}

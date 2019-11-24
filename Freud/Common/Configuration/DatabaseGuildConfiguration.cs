#region USING_DIRECTIVES

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#endregion USING_DIRECTIVES

namespace Freud.Common.Configuration
{
    [Table("guild_cfg")]
    public class DatabaseGuildConfiguration
    {
        public DatabaseGuildConfiguration()
        {
            this.Accounts = new HashSet<DatabaseBankAccount>();
            this.AntispamExempts = new HashSet<DatabaseExemptAntispam>();
            this.AutoRoles = new HashSet<DatabaseAutoRole>();
            this.Birthdays = new HashSet<DatabaseBirthday>();
            this.Chickens = new HashSet<DatabaseChicken>();
            this.ChickensBoughtUpgrades = new HashSet<DatabaseChickenBoughtUpgrade>();
            this.EmojiReactions = new HashSet<DatabaseEmojiReaction>();
            this.Filters = new HashSet<DatabaseFilter>();
            this.LoggingExempts = new HashSet<DatabaseExemptLogging>();
            this.Memes = new HashSet<DatabaseMeme>();
            this.PurchasableItems = new HashSet<DatabasePurchasableItem>();
            this.Ranks = new HashSet<DatabaseGuildRank>();
            this.RatelimitExempts = new HashSet<DatabaseExemptRatelimit>();
            this.SavedTasks = new HashSet<DatabaseSavedTask>();
            this.SelfRoles = new HashSet<DatabaseSelfRole>();
            this.Subscriptions = new HashSet<DatabaseRssSubscription>();
            this.TextReactions = new HashSet<DatabaseTextReaction>();
        }

        [Key]
        [Column("gid")]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long GuildIdDb { get; set; }

        [NotMapped]
        public ulong GuildId { get => (ulong)this.GuildIdDb; set => this.GuildIdDb = (long)value; }

        [Column("prefix"), MaxLength(16)]
        public string Prefix { get; set; }

        [Column("currency"), MaxLength(32)]
        public string Currency { get; set; }

        [Column("suggestions_enabled")]
        public bool SuggestionsEnabled { get; set; }

        [Column("log_cid")]
        public long? LogChannelIdDb { get; set; }

        [NotMapped]
        public ulong LogChannelId { get => (ulong)this.LogChannelIdDb.GetValueOrDefault(); set => this.LogChannelIdDb = (long)value; }

        [NotMapped]
        public bool LoggingEnabled => this.LogChannelId != default;

        [Column("mute_rid")]
        public long? MuteRoleIdDb { get; set; }

        [NotMapped]
        public ulong MuteRoleId { get => (ulong)this.MuteRoleIdDb.GetValueOrDefault(); set => this.MuteRoleIdDb = (long)value; }

        [Column("silent_response_enabled")]
        public bool ReactionResponse { get; set; }

        #region MEMBER_UPDATES

        //

        #endregion MEMBER_UPDATES

        #region LINKFILTER

        //

        #endregion LINKFILTER

        #region ANTIFLOOD

        //

        #endregion ANTIFLOOD

        #region ANTIINSTANTLEAVE

        //

        #endregion ANTIINSTANTLEAVE

        #region RATELIMIT

        //

        #endregion RATELIMIT

        [NotMapped]
        public CachedGuildConfiguration CachedConfiguration
        {
            get => new CachedConfiguration
            {
                AntispamSettings = this.AntispamSettings,
                Currency = this.Currency,
                LinkfilterSettings = this.LinkfilterSettings,
                LogChannelId = this.LogChannelId,
                Prefix = this.Prefix,
                RatelimitSettings = this.RatelimitSettings,
                ReactionResponse = this.ReactionResponse,
                SuggestionsEnabled = this.SuggestionsEnabled
            };
            set
            {
                this.AntispamSettings = value.AntispamSettings;
                this.Currency = value.Currency;
                this.LinkfilterSettings = value.LinkfilterSettings;
                this.LogChannelId = value.LogChannelId;
                this.Prefix = value.Prefix;
                this.RatelimitSettings = value.RatelimitSettings;
                this.ReactionResponse = value.ReactionResponse;
                this.SuggestionsEnabled = value.SuggestionsEnabled;
            }
        }

        public virtual ICollection<DatabaseBankAccount> Accounts { get; set; }
        public virtual ICollection<DatabaseExemptAntispam> AntispamExempts { get; set; }
        public virtual ICollection<DatabaseAutoRole> AutoRoles { get; set; }
        public virtual ICollection<DatabaseBirthday> Birthdays { get; set; }
        public virtual ICollection<DatabaseChicken> Chickens { get; set; }
        public virtual ICollection<DatabaseChickenBoughtUpgrade> ChickensBoughtUpgrades { get; set; }
        public virtual ICollection<DatabaseEmojiReaction> EmojiReactions { get; set; }
        public virtual ICollection<DatabaseFilter> Filters { get; set; }
        public virtual ICollection<DatabaseExemptLogging> LoggingExempts { get; set; }
        public virtual ICollection<DatabaseMeme> Memes { get; set; }
        public virtual ICollection<DatabasePurchasableItem> PurchasableItems { get; set; }
        public virtual ICollection<DatabaseGuildRank> Ranks { get; set; }
        public virtual ICollection<DatabaseExemptRatelimit> RatelimitExempts { get; set; }
        public virtual ICollection<DatabaseSavedTask> SavedTasks { get; set; }
        public virtual ICollection<DatabaseSelfRole> SelfRoles { get; set; }
        public virtual ICollection<DatabaseRssSubscription> Subscriptions { get; set; }
        public virtual ICollection<DatabaseTextReaction> TextReactions { get; set; }
    }
}
}

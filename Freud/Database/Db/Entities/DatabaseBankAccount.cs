using Freud.Common.Configuration;
using System.ComponentModel.DataAnnotations.Schema;

namespace Freud.Database.Db.Entities
{
    [Table("bank_accounts")]
    public class DatabaseBankAccount
    {
        [NotMapped]
        public static readonly int StartingBalance = 10000;

        [Column("uid")]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long UserIdDb { get; set; }

        [NotMapped]
        public ulong UserId { get => (ulong)this.UserIdDb; set => this.UserIdDb = (long)value; }

        [ForeignKey("DbGuildConfiguration")]
        [Column("gid")]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long GuildIdDb { get; set; }

        [NotMapped]
        public ulong GuildId { get => (ulong)this.GuildIdDb; set => this.GuildIdDb = (long)value; }

        [Column("balance")]
        public long Balance { get; set; } = StartingBalance;

        public virtual DatabaseGuildConfiguration DbGuildConfiguration { get; set; }
    }
}

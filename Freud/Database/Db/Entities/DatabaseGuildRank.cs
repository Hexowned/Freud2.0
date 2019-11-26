using Freud.Common.Configuration;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Freud.Database.Db.Entities
{
    [Table("guild_ranks")]
    public class DatabaseGuildRank
    {
        [ForeignKey("DbGuildConfiguration")]
        [Column("gid")]
        public long GuildIdDb { get; set; }

        [NotMapped]
        public ulong GuildId { get => (ulong)this.GuildIdDb; set => this.GuildIdDb = (long)value; }

        [Column("rank")]
        public short Rank { get; set; }

        [Column("name"), Required, MaxLength(32)]
        public string Name { get; set; }

        public virtual DatabaseGuildConfiguration DbGuildConfiguration { get; set; }
    }
}

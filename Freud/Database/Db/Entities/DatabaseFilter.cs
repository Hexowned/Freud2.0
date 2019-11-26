#region USING_DIRECTIVES

using Freud.Common.Configuration;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#endregion USING_DIRECTIVES

namespace Freud.Database.Db.Entities
{
    [Table("filters")]
    public class DatabaseFilter
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [ForeignKey("DatabaseGuildConfiguration")]
        [Column("gid")]
        public long GuildIdDb { get; set; }

        [NotMapped]
        public ulong GuildId { get => (ulong)this.GuildIdDb; set => this.GuildIdDb = (long)value; }

        [Column("trigger"), Required, MaxLength(128)]
        public string Trigger { get; set; }

        public virtual DatabaseGuildConfiguration DatabaseGuildConfiguration { get; set; }
    }
}

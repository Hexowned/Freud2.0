﻿#region USING_DIRECTIVES
using Sharper.Common.Configuration;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#endregion

namespace Sharper.Database.Entities
{
    [Table("filters")]
    public class DatabaseFilter
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [ForeignKey("DatabaseGuildConfig")]
        [Column("gid")]
        public long GuildIdDb { get; set; }
        [NotMapped]
        public ulong GuildId { get => (ulong)this.GuildIdDb; set => this.GuildIdDb = (long)value; }

        [Column("trigger"), Required, MaxLength(128)]
        public string Trigger { get; set; }

        public virtual DatabaseGuildConfiguration DatabaseGuildConfig { get; set; }
    }
}

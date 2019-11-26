#region USING_DIRECTIVES

using Freud.Common.Configuration;
using Freud.Extensions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

#endregion USING_DIRECTIVES

namespace Freud.Database.Db.Entities
{
    [Table("forbidden_names")]
    public class DatabaseForbiddenName
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [ForeignKey("DbGuildConfiguration")]
        [Column("gid")]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long GuildIdDb { get; set; }

        [NotMapped]
        public ulong GuildId { get => (ulong)this.GuildIdDb; set => this.GuildIdDb = (long)value; }

        [Column("name_regex"), Required, MaxLength(64)]
        public string RegexString { get; set; }

        [NotMapped]
        public Regex Regex => this.RegexString.CreateWordBoundaryRegex();

        public virtual DatabaseGuildConfiguration DbGuildConfiguration { get; set; }
    }
}

#region USING_DIRECTIVES

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#endregion USING_DIRECTIVES

namespace Freud.Database.Db.Entities
{
    [Table("insults")]
    public class DatabaseInsult
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("content"), Required, MaxLength(128)]
        public string Content { get; set; }
    }
}

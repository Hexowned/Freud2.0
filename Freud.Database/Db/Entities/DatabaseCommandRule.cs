#region USING_DIRECTIVES

using System.ComponentModel.DataAnnotations.Schema;

#endregion USING_DIRECTIVES

namespace Freud.Database.Db.Entities
{
    [Table("cmd_rules")]
    public class DatabaseCommandRule
    {
        public virtual DatabaseGuildConfiguration DatabaseGuildConfiguration { get; set; }
    }
}

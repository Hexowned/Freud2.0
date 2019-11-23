#region USING_DIRECTIVES
using System.ComponentModel.DataAnnotations.Schema;
using Sharper.Common.Configuration;
#endregion;

namespace Sharper.Database.Entities
{
    [Table("cmd_rules")]
    public class DatabaseCommandRule
    {

        public virtual DatabaseGuildConfiguration DatabaseGuildConfig { get; set; }
    }
}

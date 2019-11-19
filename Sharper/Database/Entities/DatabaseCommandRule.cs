#region USING_DIRECTIVES
using Sharper.Common.Configuration;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#endregion;

namespace Sharper.Database.Entities
{
    [Table("cmd_rules")]
    public class DatabaseCommandRule
    {

        public virtual DatabaseGuildConfiguration DatabaseGuildConfig { get; set; }
    }
}

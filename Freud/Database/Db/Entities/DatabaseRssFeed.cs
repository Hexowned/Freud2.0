#region USING_DIRECTIVES

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#endregion USING_DIRECTIVES

namespace Freud.Database.Db.Entities
{
    [Table("rss-feeds")]
    public class DatabaseRssFeed
    {
        public DatabaseRssFeed()
        {
            this.Subscriptions = new HashSet<DatabaseRssSubscription>();
        }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("url"), Required]
        public string Url { get; set; }

        [Column("last_post_url"), Required]
        public string LastPostUrl { get; set; }

        public virtual ICollection<DatabaseRssSubscription> Subscriptions { get; set; }
    }
}

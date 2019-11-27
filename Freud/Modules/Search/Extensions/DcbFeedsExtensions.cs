#region USING_DIRECTIVES

using Freud.Database.Db;
using Freud.Database.Db.Entities;
using Freud.Modules.Search.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Search.Extensions
{
    public static class DatabaseContextBuilderFeedsExtensions
    {
        public static async Task SubscribeAsync(this DatabaseContextBuilder dcb, ulong gid, ulong cid, string url, string name = null)
        {
            var newest = RssService.GetFeedResults(url)?.FirstOrDefault();
            if (newest is null)
                throw new Exception("Can't load the feed entries!");

            using (var dc = dcb.CreateContext())
            {
                var feed = dc.RssFeeds.SingleOrDefault(f => f.Url == url);
                if (feed is null)
                {
                    feed = new DatabaseRssFeed
                    {
                        Url = url,
                        LastPostUrl = newest.Links[0].Uri.ToString()
                    };

                    dc.RssFeeds.Add(feed);
                    await dc.SaveChangesAsync();
                }

                dc.RssSubscriptions.Add(new DatabaseRssSubscription
                {
                    ChannelId = cid,
                    GuildId = gid,
                    Id = feed.Id,
                    Name = name ?? url
                });

                await dc.SaveChangesAsync();
            }
        }
    }
}

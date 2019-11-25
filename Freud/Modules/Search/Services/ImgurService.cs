#region USING_DIRECTIVES

using Freud.Services;
using Imgur.API.Authentication.Impl;
using Imgur.API.Endpoints.Impl;
using Imgur.API.Enums;
using Imgur.API.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Search.Services
{
    public class ImgurService : IFreudService
    {
        private readonly ImgurClient imgur;
        private readonly GalleryEndpoint gEndPoint;

        public ImgurService(string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                this.imgur = new ImgurClient(key);
                this.gEndPoint = new GalleryEndpoint(this.imgur);
            }
        }

        public bool IsDisabled()
            => this.imgur is null;

        public async Task<IEnumerable<IGalleryItem>> GetItemsFromSubAsync(string sub, int amount, SubredditGallerySortOrder order, TimeWindow time)
        {
            if (this.IsDisabled())
                return null;

            if (string.IsNullOrWhiteSpace(sub))
                throw new ArgumentException("Subreddit missing!", nameof(sub));

            if (amount < 1 || amount > 10)
                throw new ArgumentException("Result amount out of range (max 10)", nameof(amount));

            IEnumerable<IGalleryItem> images = await this.gEndPoint.GetSubredditGalleryAsync(sub, order, time).ConfigureAwait(false);

            return images.Take(amount);
        }
    }
}

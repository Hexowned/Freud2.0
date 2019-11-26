#region USING_DIRECTIVES

using Freud.Modules.Search.Common;
using Freud.Services;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Search.Services
{
    public class GoodreadsService : FreudHttpService
    {
        private static readonly string _url = "";
        private static readonly XmlSerializer _serializer = new XmlSerializer(typeof(GoodreadsResponse));
        private static readonly SemaphoreSlim _requestSemaphore = new SemaphoreSlim(1, 1);

        private readonly string key;

        public GoodreadsService(string key)
        {
            this.key = key;
        }

        public override bool IsDisabled()
            => string.IsNullOrWhiteSpace(this.key);

        public async Task<GoodreadsSearchInfo> SearchBooksAsync(string query)
        {
            if (this.IsDisabled())
                return null;
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Query missing.", nameof(query));

            await _requestSemaphore.WaitAsync();
            try
            {
                using (var stream = await _http.GetStreamAsync($"{_url}?key={this.key}&q={WebUtility.UrlEncode(query)}").ConfigureAwait(false))
                {
                    var response = (GoodreadsResponse)_serializer.Deserialize(stream);

                    return response.SearchInfo;
                }
            } catch
            {
                return null;
            } finally
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                _requestSemaphore.Release();
            }
        }
    }
}

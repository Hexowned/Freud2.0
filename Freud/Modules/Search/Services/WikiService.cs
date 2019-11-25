#region USING_DIRECTIVES

using Freud.Modules.Search.Common;
using Freud.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Search.Services
{
    public class WikiService : FreudHttpService
    {
        public static readonly string WikipediaIconUrl = "https://en.wikipedia.org/static/images/project-logos/enwiki.png";

        private static readonly string _url = "https://en.wikipedia.org/w/api.php?action=opensearch&limit=20&namespace=0&format=json&search=";
        private static readonly SemaphoreSlim _requestSemaphore = new SemaphoreSlim(1, 1);

        public override bool IsDisabled()
            => false;

        public static async Task<WikiSearchResponse> SearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Query missing", nameof(query));

            string result = await _http.GetStringAsync($"{_url}{WebUtility.UrlEncode(query)}").ConfigureAwait(false);

            await _requestSemaphore.WaitAsync();
            try
            {
                var jarr = JArray.Parse(result);

                var tquery = jarr.First;
                var thits = tquery.Next;
                var tsnippets = thits.Next;
                var turls = tsnippets.Next;

                return new WikiSearchResponse(tquery.ToString(), thits.ToObject<string[]>(), tsnippets.ToObject<string[]>(), turls.ToObject<string[]>());
            } catch
            {
                return null;
            } finally
            {
                _requestSemaphore.Release();
            }
        }
    }
}

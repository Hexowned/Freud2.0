#region USING_DIRECTIVES

using Freud.Modules.Search.Common;
using Freud.Services;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Search.Services
{
    public class UrbanDictionaryService : FreudHttpService
    {
        private static readonly string _url = "http://api.urbandictionary.com/v0";

        public override bool IsDisabled()
            => false;

        public static async Task<UrbanDictionaryData> GetDefinitionForTermAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Query missing!", nameof(query));

            string result = await _http.GetStringAsync($"{_url}/define?term={WebUtility.UrlEncode(query)}").ConfigureAwait(false);
            var data = JsonConvert.DeserializeObject<UrbanDictionaryData>(result);
            if (data.ResultType == "no_results" || !data.List.Any())
                return null;

            foreach (var res in data.List)
            {
                res.Definition = new string(res.Definition.ToCharArray().Where(c => c != ']' && c != '[').ToArray());
                if (!string.IsNullOrWhiteSpace(res.Example))
                    res.Example = new string(res.Example.ToCharArray().Where(c => c != ']' && c != '[').ToArray());
            }

            return data;
        }
    }
}

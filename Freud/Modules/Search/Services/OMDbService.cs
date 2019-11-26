﻿#region USING_DIRECTIVES

using DSharpPlus.Interactivity;
using Freud.Modules.Search.Common;
using Freud.Modules.Search.Extensions;
using Freud.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Search.Services
{
    public class OMDbService : FreudHttpService
    {
        private static readonly string _url = "http://www.omdbapi.com/";

        private readonly string key;

        public OMDbService(string key)
        {
            this.key = key;
        }

        public override bool IsDisabled()
            => string.IsNullOrWhiteSpace(this.key);

        public async Task<IReadOnlyList<Page>> GetPaginatedResultsAsync(string query)
        {
            if (this.IsDisabled())
                return null;

            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Query missing!", nameof(query));

            string response = await _http.GetStringAsync($"{_url}?apikey={this.key}&s={query}").ConfigureAwait(false);
            var data = JsonConvert.DeserializeObject<OMDbResponse>(response);
            IReadOnlyList<MovieInfo> results = data.Success ? data.Results?.AsReadOnly() : null;

            if (results is null || !results.Any())
                return null;

            return results.Select(info => info.ToDiscordPage()).ToList().AsReadOnly();
        }

        public async Task<MovieInfo> GetSingleResultAsync(OMDbQueryType type, string query)
        {
            if (this.IsDisabled())
                return null;

            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Query missing!", nameof(query));

            string response = await _http.GetStringAsync($"{_url}?apikey={this.key}&{type.ToApiString()}={query}").ConfigureAwait(false);
            var data = JsonConvert.DeserializeObject<MovieInfo>(response);

            return data.Success ? data : null;
        }
    }
}

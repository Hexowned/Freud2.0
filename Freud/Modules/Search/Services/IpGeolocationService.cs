#region USING_DIRECTIVES

using Freud.Modules.Search.Common;
using Freud.Services;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Search.Services
{
    public class IpGeolocationService : FreudHttpService
    {
        private static readonly string _url = "http://ip-api.com/json";

        public override bool IsDisabled()
            => false;

        public static Task<IpInfo> GetInfoForIpAsync(string ipstr)
        {
            if (string.IsNullOrWhiteSpace(ipstr))
                throw new ArgumentException("IP missing!", nameof(ipstr));

            if (!IPAddress.TryParse(ipstr, out var ip))
                throw new ArgumentException("Given string does not map to a IPv4 address.");

            return GetInfoForIpAsync(ip);
        }

        public static async Task<IpInfo> GetInfoForIpAsync(IPAddress ip)
        {
            if (ip is null)
                throw new ArgumentNullException(nameof(ip));

            string response = await _http.GetStringAsync($"{_url}/{ip.ToString()}").ConfigureAwait(false);
            var data = JsonConvert.DeserializeObject<IpInfo>(response);

            return data;
        }
    }
}

#region USE_DIRECTIVES
using System.Net.Http;
#endregion

namespace Sharper.Services
{
    public abstract class HttpService : IService
    {
        protected static readonly HttpClientHandler _handler = new HttpClientHandler { AllowAutoRedirect = false };
        protected static readonly HttpClient _http = new HttpClient(_handler, true);

        public abstract bool IsDisabled();
    }
}

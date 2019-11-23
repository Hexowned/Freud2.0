#region USE_DIRECTIVES

using System.Net.Http;

#endregion USE_DIRECTIVES

namespace Freud.Services
{
    public abstract class FreudHttpService : IFreudService
    {
        protected static readonly HttpClientHandler _handler = new HttpClientHandler { AllowAutoRedirect = false };
        protected static readonly HttpClient _http = new HttpClient(_handler, true);

        public abstract bool IsDisabled();
    }
}

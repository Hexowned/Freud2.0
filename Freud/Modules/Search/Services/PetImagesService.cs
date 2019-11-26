#region USING_DIRECTIVES

using Freud.Services;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Search.Services
{
    public class PetImagesService : FreudHttpService
    {
        public override bool IsDisabled()
            => false;

        public static async Task<string> GetRandomCatImageAsync()
        {
            string data = await _http.GetStringAsync("https://random.cat/meow").ConfigureAwait(false);

            return JObject.Parse(data)["file"].ToString();
        }

        public static async Task<string> GetRandomDogImageAsync()
        {
            string data = await _http.GetStringAsync("https://random.dog/woof").ConfigureAwait(false);

            return "https://random.dog/" + data;
        }
    }
}

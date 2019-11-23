#region USING_DIRECTIVES

using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Converters;
using DSharpPlus.Entities;

using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Common.Converters
{
    public class CustomIPFormatConverter : IArgumentConverter<CustomIPFormat>
    {
        public Task<Optional<CustomIPFormat>> ConvertAsync(string value, CommandContext ctx)
        {
            if (!CustomIPFormat.TryParse(value, out CustomIPFormat ip))
                return Task.FromResult(new Optional<CustomIPFormat>());

            return Task.FromResult(new Optional<CustomIPFormat>(ip));
        }
    }
}

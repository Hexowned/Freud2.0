#region USING_DIRECTIVES

using DSharpPlus.Entities;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Extensions.Discord
{
    internal static class DiscordUserExtensions
    {
        public static async Task<bool> IsMemberOfGuildAsync(this DiscordUser u, DiscordGuild g)
        {
            try
            {
                var m = await g.GetMemberAsync(u.Id);
                return true;
            } catch
            {
                // Not found ...
            }

            return false;
        }
    }
}

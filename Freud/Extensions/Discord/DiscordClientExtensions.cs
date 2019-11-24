#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.Entities;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Extensions.Discord
{
    internal static class DiscordClientExtensions
    {
        public static Task<DiscordDmChannel> CreateDmChannelAsync(this DiscordClient client, ulong uid)
        {
            foreach ((ulong gid, var guild) in client.Guilds)
            {
                if (guild.Members.TryGetValue(uid, out var member))
                    return member?.CreateDmChannelAsync() ?? Task.FromResult<DiscordDmChannel>(null);
            }

            return Task.FromResult<DiscordDmChannel>(null);
        }
    }
}

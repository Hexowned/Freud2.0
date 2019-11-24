#region USING_DIRECTIVES

using DSharpPlus.Entities;

#endregion USING_DIRECTIVES

namespace Freud.Extensions.Discord
{
    public static class DiscordWebhookExtension
    {
        public static string BuildUrlString(this DiscordWebhook wh)
            => $"https://discordapp.com/api/webhooks/ {wh.ChannelId }/{wh.Token }";
    }
}

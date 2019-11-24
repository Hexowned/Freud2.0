#region USING_DIRECTIVES

using DSharpPlus.Entities;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Administration.Extensions
{
    public static class DiscordClientStatusExtension
    {
        public static string ToUserFriendlyString(this DiscordClientStatus status)
        {
            if (status.Desktop.HasValue)
                return "Desktop";
            else if (status.Mobile.HasValue)
                return "Mobile";
            else if (status.Web.HasValue)
                return "Web";
            else
                return "Unknown";
        }
    }
}

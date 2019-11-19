#region USING_DIRECTIVES
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
#endregion

namespace Sharper.Common
{
    public abstract class ChannelEvent
    {
        public DiscordChannel Channel { get; protected set; }
        public InteractivityExtension Interactivity { get; protected set; }
        public bool IsTimeoutReached { get; protected set; }
        public DiscordUser Winner { get; protected set; }

        protected ChannelEvent(InteractivityExtension interactivity, DiscordChannel channel)
        {
            this.Interactivity = interactivity;
            this.Channel = channel;
        }

        public abstract Task RunAsync();
    }
}

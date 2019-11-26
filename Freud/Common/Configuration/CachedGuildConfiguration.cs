using Freud.Modules.Administration.Services;

namespace Freud.Common.Configuration
{
    public sealed class CachedGuildConfiguration
    {
        public string Currency { get; set; }
        public string Prefix { get; set; }
        public ulong LogChannelId { get; set; }
        public bool SuggestionsEnabled { get; set; }
        public bool ReactionResponse { get; set; }

        public bool LoggingEnabled => this.LogChannelId != default;

        public LinkfilterSettings LinkfilterSettings { get; set; }
        public AntispamSettings AntispamSettings { get; set; }
        public RatelimitSettings RatelimitSettings { get; set; }

        public static CachedGuildConfiguration Default => new CachedGuildConfiguration
        {
            AntispamSettings = new AntispamSettings(),
            RatelimitSettings = new RatelimitSettings(),
            LinkfilterSettings = new LinkfilterSettings(),
            SuggestionsEnabled = false,
            ReactionResponse = false,
            LogChannelId = default,
            Prefix = null
        };
    }
}

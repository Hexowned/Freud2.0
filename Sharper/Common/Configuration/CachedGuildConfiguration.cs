#region USING_DIRECTIVES
#endregion

namespace Sharper.Common.Configuration
{
    public sealed class CachedGuildConfiguration
    {
        public string Currency { get; set; }
        public string Prefix { get; set; }
        public ulong LogChannelId { get; set; }
        public bool SuggestionEnabled { get; set; }
        public bool ReactionResponse { get; set; }


        public bool LoggingEnabled => this.LogChannelId != default;

        public static CachedGuildConfiguration Default => new CachedGuildConfiguration
        {

        };
    }
}

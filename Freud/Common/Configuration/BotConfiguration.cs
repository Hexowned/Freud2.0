#region USING_DIRECTIVES

using DSharpPlus;
using Freud.Database.Db;
using Newtonsoft.Json;
using System.Collections.Generic;

#endregion USING_DIRECTIVES

namespace Freud.Common.Configuration
{
    public sealed class BotConfiguration
    {
        [JsonProperty("db-config")]
        public DatabaseConfiguration DatabaseConfiguration { get; private set; }

        [JsonProperty("db_sync_interval")]
        public int DatabaseSyncInterval { get; private set; }

        [JsonProperty("prefix")]
        public string DefaultPrefix { get; private set; }

        [JsonProperty("feed_check_interval")]
        public int FeedCheckInterval { get; private set; }

        [JsonProperty("feed_check_start_delay")]
        public int FeedCheckStartDelay { get; private set; }

        [JsonProperty("key-giphy")]
        public string GiphyKey { get; private set; }

        [JsonProperty("key-goodreads")]
        public string GoodreadsKey { get; private set; }

        [JsonProperty("key-wolfram")]
        public string WolframKey { get; private set; }

        [JsonProperty("key-imgur")]
        public string ImgurKey { get; private set; }

        [JsonProperty("log-level")]
        public LogLevel LogLevel { get; private set; }

        [JsonProperty("log-path")]
        public string LogPath { get; private set; }

        [JsonProperty("log-to-file")]
        public bool LogToFile { get; private set; }

        [JsonProperty("key-omdb")]
        public string OMDbKey { get; private set; }

        [JsonProperty("shard-count")]
        public int ShardCount { get; private set; }

        [JsonProperty("key-steam")]
        public string SteamKey { get; private set; }

        [JsonProperty("key-weather")]
        public string WeatherKey { get; private set; }

        [JsonProperty("key-youtube")]
        public string YouTubeKey { get; private set; }

        [JsonProperty("token")]
        public string Token { get; private set; }

        [JsonProperty("logger-special-rules")]
        public List<Logger.SpecialLoggingRule> SpecialLoggerRules { get; private set; }

        [JsonIgnore]
        public static BotConfiguration Default => new BotConfiguration
        {
            DatabaseConfiguration = DatabaseConfiguration.Default,
            DatabaseSyncInterval = 600,
            DefaultPrefix = "!",
            FeedCheckInterval = 300,
            FeedCheckStartDelay = 30,
            GiphyKey = "<insert GIPHY API key>",
            GoodreadsKey = "<insert Goodreads API key>",
            SpecialLoggerRules = new List<Logger.SpecialLoggingRule>(),
            ImgurKey = "<insert Imgur API key>",
            LogLevel = LogLevel.Info,
            LogPath = "log.txt",
            LogToFile = false,
            OMDbKey = "<insert OMDb API key>",
            ShardCount = 1,
            SteamKey = "<insert Steam API key>",
            Token = "<insert Bot token key>",
            WeatherKey = "<insert OpenWeatherMaps API key>",
            YouTubeKey = "<insert YouTube API key>",
            WolframKey = "<insert Wolfram API key>"
        };
    }
}

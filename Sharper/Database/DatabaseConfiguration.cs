#region DIRECTIVES
using Newtonsoft.Json;
#endregion

namespace Sharper.Database
{
    public sealed class DatabaseConfiguration
    {
        public enum DatabaseProvider
        {
            SQLite = 0,
            PostgreSQL = 1,
            SQLServer = 2,
            MySQL = 3
        }

        [JsonProperty("database")]
        public string DatabaseName { get; set; }

        [JsonProperty("provider")]
        public DatabaseProvider Provider { get; set; }

        [JsonProperty("hostname")]
        public string Hostname { get; set; }

        [JsonProperty("port")]
        public int Port { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }

        [JsonIgnore]
        public static DatabaseConfiguration Default => new DatabaseConfiguration
        {
            DatabaseName = "Sharper",
            Provider = DatabaseProvider.SQLite,
            Hostname = "localhost",
            Password = "dev2019",
            Port = 5000,
            Username = ""
        };
    }
}

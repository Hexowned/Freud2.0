#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.EventArgs;
using Freud.Common;
using Freud.Common.Attributes;
using Freud.Common.Configuration;
using Freud.Database.Db;
using Freud.Extensions.Discord;
using System;
using System.Linq;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.EventListeners
{
    internal static partial class Listener
    {
        [AsyncEventListener(DiscordEventType.ClientErrored)]
        public static Task ClientErrorEventHandlerAsync(FreudShard shard, ClientErrorEventArgs e)
        {
            var ex = e.Exception;
            while (ex is AggregateException)
                ex = ex.InnerException;

            if (ex.InnerException is null)
            {
                shard.LogMany(LogLevel.Critical, $"Client errored with exception: {ex.GetType()}", $"Message: {ex.Message}");
            } else
            {
                shard.LogMany(LogLevel.Critical,
                    $"Client errored with exception: {ex.GetType()}",
                    $"Message: {ex.Message}",
                    $"Inner exception: {ex.InnerException.GetType()}",
                    $"Inner exception message: {ex.InnerException.Message}");
            };

            return Task.CompletedTask;
        }

        [AsyncEventListener(DiscordEventType.GuildAvailable)]
        public static Task GuildAvailableEventHandlerAsync(FreudShard shard, GuildCreateEventArgs e)
        {
            shard.Log(LogLevel.Debug, $"Guild available: {e.Guild.ToString()}");
            if (shard.SharedData.GuildConfigurations.ContainsKey(e.Guild.Id))
                return Task.CompletedTask;

            return RegisterGuildAsync(shard.SharedData, shard.Database, e.Guild.Id);
        }

        [AsyncEventListener(DiscordEventType.GuildDownloadCompleted)]
        public static Task GuildDownloadCompletedEventHandlerAsync(FreudShard shard, GuildDownloadCompletedEventArgs e)
        {
            shard.Log(LogLevel.Info, $"All guilds are now available.");

            return Task.CompletedTask;
        }

        [AsyncEventListener(DiscordEventType.GuildCreated)]
        public static async Task GuildCreatedEventhandlerAsync(FreudShard shard, GuildCreateEventArgs e)
        {
            shard.Log(LogLevel.Info, $"Joined guild: {e.Guild.ToString()}");
            await RegisterGuildAsync(shard.SharedData, shard.Database, e.Guild.Id);

            var defChannel = e.Guild.GetDefaultChannel();
            if (!defChannel.PermissionsFor(e.Guild.CurrentMember).HasPermission(Permissions.SendMessages))
                return;
            await defChannel.EmbedAsync(
                $"{Formatter.Bold("Thank you for adding me to your discord!")}\n\n" +
                $"{StaticDiscordEmoji.SmallBlueDiamond} The default prefix for my command is {Formatter.Bold(shard.SharedData.BotConfiguration.DefaultPrefix)}, but it can be changed using {Formatter.Bold("prefix")} command.\n" +
                $"{StaticDiscordEmoji.SmallBlueDiamond} I advise you to run the configuration wizard for this guild in order to quickly configure functions like logging and notifications. The wizard can be invoked using {Formatter.Bold("guild configuration setup")} command.\n" +
                $"{StaticDiscordEmoji.SmallBlueDiamond} You can use the {Formatter.Bold("help")} command as a guide, though it is recommended to read the command list provided in the source \n" +
                $"{StaticDiscordEmoji.SmallBlueDiamond} If you have any questions or issues, use the {Formatter.Bold("report")} command in order to send a message to the bot owner ({e.Client.CurrentApplication.Team}#{e.Client.CurrentApplication.Description})." + StaticDiscordEmoji.Wave
                );
        }

        private static async Task RegisterGuildAsync(SharedData shared, DatabaseContextBuilder dcb, ulong gid)
        {
            shared.GuildConfigurations.TryAdd(gid, CachedGuildConfiguration.Default);
            using (var dc = dcb.CreateContext())
            {
                var gcfg = new DatabaseGuildConfiguration { GuildId = gid };
                if (!dc.GuildConfiguration.Contains(gcfg))
                {
                    dc.GuildConfiguration.Add(gcfg);
                    await dc.SaveChangesAsync();
                }
            }
        }
    }
}

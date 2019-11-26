#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.Entities;
using Freud.Common;
using Freud.Common.Collections;
using Freud.Common.Configuration;
using Freud.Common.Tasks;
using Freud.Database.Db;
using Freud.Database.Db.Entities;
using Freud.Extensions;
using Freud.Modules.Administration.Common;
using Freud.Modules.Reactions;
using Freud.Modules.Search.Services;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud
{
    internal static class Freud
    {
        public static readonly string ApplicationName = "Freud";
        public static readonly string ApplicationVersion = "v1";
        public static IReadOnlyList<FreudShard> ActiveShards => Shards.AsReadOnly();

        private static BotConfiguration BotConfiguration { get; set; }
        private static DatabaseContextBuilder GlobalDatabaseContextBuilder { get; set; }
        private static List<FreudShard> Shards { get; set; }
        private static SharedData SharedData { get; set; }

        #region TIMERS

        private static Timer BotStatusUpdateTimer { get; set; }
        private static Timer DatabaseSyncTimer { get; set; }
        private static Timer FeedCheckTimer { get; set; }
        private static Timer MiscActionsTimer { get; set; }

        #endregion TIMERS

        #region ENTRY_POINT

        internal static async Task Main(string[] _)
        {
            try
            {
                PrintBuildInformation();

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                await LoadBotConfigurationAsync();
                await InitializeDatabaseAsync();
                LoadSharedDataFromDatabase();

                await CreateAndBootShardsAsync();
                SharedData.LogProvider.ElevatedLog(LogLevel.Info, "Booting complete! Registering timers and saved tasks...");

                try
                {
                    await Task.Delay(Timeout.Infinite, SharedData.MainLoopCts.Token);
                } catch (TaskCanceledException)
                {
                    SharedData.LogProvider.ElevatedLog(LogLevel.Info, "Shutdown signal recieved!");
                }

                await DisposeAsync();
            } catch (Exception e)
            {
                Console.WriteLine($"\nException occured: {e.GetType()} :\n{e.Message}");

                if (!(e.InnerException is null))
                    Console.WriteLine($"Inner exception: {e.InnerException.GetType()} :\n{e.InnerException.Message}");
                Console.ReadKey();
                Environment.ExitCode = 1;
            }

            Console.WriteLine("\nPowering off...");
            Environment.Exit(Environment.ExitCode);
        }

        internal static Task Stop(int exitCode = 0, TimeSpan? after = null)
        {
            Environment.ExitCode = exitCode;
            SharedData.MainLoopCts.CancelAfter(after ?? TimeSpan.Zero);

            return Task.CompletedTask;
        }

        #endregion ENTRY_POINT

        #region SETUP_FUNCTIONS

        private static void PrintBuildInformation()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);

            Console.WriteLine($"{ApplicationName} {ApplicationVersion} ({fileVersionInfo.FileVersion})");
            Console.WriteLine();
        }

        private static async Task LoadBotConfigurationAsync()
        {
            Console.Write("\r[1/5] Loading configuration...             ");

            string json = "{}";
            var utf8 = new UTF8Encoding(false);
            var fi = new FileInfo("Freud.Resources/configuration.json");

            if (!fi.Exists)
            {
                Console.WriteLine("\rLoading configuration failed!... Trying to create a fresh one!");

                json = JsonConvert.SerializeObject(BotConfiguration.Default, Formatting.Indented);
                using (var fs = fi.Create())
                using (var sw = new StreamWriter(fs, utf8))
                {
                    await sw.WriteAsync(json);
                    await sw.FlushAsync();
                }

                Console.WriteLine("New default configuration file has been created at:");
                Console.WriteLine(fi.FullName);
                Console.WriteLine("Please fill it with the appropriate values and re-run the bot.");

                throw new IOException("Configuration file not found!");
            }

            using (var fs = fi.OpenRead())
            using (var sr = new StreamReader(fs, utf8))
                json = await sr.ReadToEndAsync();

            BotConfiguration = JsonConvert.DeserializeObject<BotConfiguration>(json);
        }

        private static async Task InitializeDatabaseAsync()
        {
            Console.Write("\r[2/5] Establishing database connection...          ");

            GlobalDatabaseContextBuilder = new DatabaseContextBuilder(BotConfiguration.DatabaseConfiguration);
            await GlobalDatabaseContextBuilder.CreateContext().Database.MigrateAsync();
        }

        private static LoadSharedDataFromDatabase()
        {
            Console.Write("\r[3/5] Loading data from database...                ");

            ConcurrentHashSet<ulong> blockedChannels;
            ConcurrentHashSet<ulong> blockedUsers;
            ConcurrentDictionary<ulong, CachedGuildConfiguration> guildConfigurations;
            ConcurrentDictionary<ulong, ConcurrentHashSet<Filter>> filters;
            ConcurrentDictionary<ulong, ConcurrentHashSet<TextReaction>> treactions;
            ConcurrentDictionary<ulong, ConcurrentHashSet<EmojiReaction>> ereactions;
            ConcurrentDictionary<ulong, int> msgcount;

            using (var dc = GlobalDatabaseContextBuilder.CreateContext())
            {
                blockedChannels = new ConcurrentHashSet<ulong>(dc.BlockedChannels.Select(c => c.ChannelId));
                blockedUsers = new ConcurrentHashSet<ulong>(dc.BlockedUsers.Select(u => u.UserId));
                guildConfigurations = new ConcurrentDictionary<ulong, CachedGuildConfiguration>(dc.GuildConfiguration.Select(
                    gcfg => new KeyValuePair<ulong, CachedGuildConfiguration>(gcfg.GuildId, new CachedGuildConfiguration
                    {
                        AntispamSettings = new AntispamSettings
                        {
                            Action = gcfg.AntispamAction,
                            Enabled = gcfg.AntispamEnabled,
                            Sensitivity = gcfg.AntispamSensitivity
                        },
                        Currency = gcfg.Currency,
                        LinkfilterSettings = new LinkfilterSettings
                        {
                            BlockBooterWebsites = gcfg.LinkfilterBootersEnabled,
                            BlockDiscordInvites = gcfg.LinkfilterDiscordInvitesEnabled,
                            BlockDisturbingWebsites = gcfg.LinkfilterDisturbingWebsitesEnabled,
                            BlockIpLoggingWebsites = gcfg.LinkfilterIpLoggersEnabled,
                            BlockUrlShorteners = gcfg.LinkfilterUrlShortenersEnabled,
                            Enabled = gcfg.LinkfilterEnabled
                        },
                        LogChannelId = gcfg.LogChannelId,
                        Prefix = gcfg.Prefix,
                        RatelimitSettings = new RatelimitSettings
                        {
                            Action = gcfg.RatelimitAction,
                            Enabled = gcfg.RatelimitEnabled,
                            Sesitivity = gcfg.RatelimitSensitivity
                        },
                        ReactionResponse = gcfg.ReactionResponse,
                        SuggestionEnabled = gcfg.SuggestionsEnabled
                    })));
                filters = new ConcurrentDictionary<ulong, ConcurrentHashSet<Filter>>(
                    dc.Filters.GroupBy(f => f.GuildId)
                    .ToDictionary(g => g.Key, g =>
                    new ConcurrentHashSet<Filter>(g.Select(f => new EventTypeFilter(f.Id, f.Trigger)))));

                msgcount = new ConcurrentDictionary<ulong, int>(
                    dc.MessageCount.GroupBy(ui => ui.UserId)
                    .ToDictionary(g => g.Key, g => g.First().MessageCount));

                treactions = new ConcurrentDictionary<ulong, ConcurrentHashSet<TextReaction>>(
                    dc.TextReactions.Include(t => t.DbTriggers)
                    .AsEnumerable().GroupBy(er => er.GuildId)
                    .ToDictionary(g => g.Key, g => new ConcurrentHashSet<EmojiReaction>(g.Select(er =>
                    new EmojiReaction(er.Id, er.Triggers, er.Reaction, true)))));
            }

            var logger = new Logger(BotConfiguration);
            foreach (var rule in BotConfiguration.SpecialLoggerRules)
                logger.ApplySpecialLoggingRule(rule);

            SharedData = new SharedData
            {
                BlockedChannels = blockedChannels,
                BlockedUsers = blockedUsers,
                BotConfiguration = BotConfiguration,
                MainLoopCts = new CancellationTokenSource(),
                EmojiReactions = ereactions,
                Filters = filters,
                GuildConfigurations = guildConfigurations,
                LogProvider = logger,
                MessageCount = msgcount,
                TextReactions = treactions,
                UptimeInformation = new UptimeInformation(Process.GetCurrentProcess().StartTime)
            };
        }

        private static Task CreateAndBootShardsAsync()
        {
            Console.Write($"[4/5] Creating {BotConfiguration.ShardCount} shards...              ");

            Shards = new List<FreudShard>();
            for (int i = 0; i < BotConfiguration.ShardCount; i++)
            {
                var shard = new FreudShard(i, GlobalDatabaseContextBuilder, SharedData);
                shard.Initialize(async e => await RegisterPeriodicTasksAsync());
                Shards.Add(shard);
            }

            Console.WriteLine("\r[5/5] Booting the shards...                ");
            Console.WriteLine();

            return Task.WhenAll(Shards.Select(s => s.StartAsync()));
        }

        private static async Task RegisterPeriodicTasksAsync()
        {
            BotStatusUpdateTimer = new Timer(BotActivityCallback, Shards[0].Client, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(10));
            DatabaseSyncTimer = new Timer(DatabaseSyncCallback, Shards[0].Client, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(BotConfiguration.DatabaseSyncInterval));
            FeedCheckTimer = new Timer(FeedCheckCallback, Shards[0].Client, TimeSpan.FromSeconds(BotConfiguration.FeedCheckStartDelay), TimeSpan.FromSeconds(BotConfiguration.FeedCheckInterval));
            MiscActionsTimer = new Timer(MiscellaneousActionsCallback, Shards[0].Client, TimeSpan.FromSeconds(5), TimeSpan.FromHours(12));

            using (var dc = GlobalDatabaseContextBuilder.CreateContext())
            {
                await RegisterSavedTasksAsync(dc.SavedTasks.ToDictionary<DatabaseSavedTask, int, SavedTaskInfo>(
                    t => t.Id, t =>
                    {
                        switch (t.Type)
                        {
                            case SavedTaskType.Unban:
                                return new UnbanTaskInfo(t.GuildId, t.UserId, t.ExecutionTime);

                            case SavedTaskType.Unmute:
                                return new UnmuteTaskInfo(t.GuildId, t.UserId, t.RoleId, t.ExecutionTime);

                            default:
                                return null;
                        }
                    }));
                await RegisterReminderAsync(dc.Reminders.ToDictionary(
                    t => t.Id, t =>
                    new SendMessageTaskInfo(t.ChannelId, t.UserId, t.Message, t.ExecutionTime, t.IsRepeating, t.RepeatInterval)));
            }

            async Task RegisterSavedTasksAsync(IReadOnlyDictionary<int, SavedTaskInfo> tasks)
            {
                int scheduled = 0, missed = 0;
                foreach ((int tid, var task) in tasks)
                {
                    if (await RegisterTaskAsync(tid, task))
                        scheduled++;
                    else
                        missed++;
                }

                SharedData.LogProvider.ElevatedLog(LogLevel.Info, $"Saved tasks: {scheduled} scheduled; {missed} missed.");
            }

            async Task RegisterReminderAsync(IReadOnlyDictionary<int, SendMessageTaskInfo> reminders)
            {
                int scheduled = 0, missed = 0;
                foreach ((int tid, var task) in reminders)
                {
                    if (await RegisterTaskAsync(tid, task))
                        scheduled++;
                    else
                        missed++;
                }

                SharedData.LogProvider.ElevatedLog(LogLevel.Info, $"Reminder: {scheduled} scheduled; {missed} missed.");
            }

            async Task<bool> RegisterTaskAsync(int id, SavedTaskInfo tinfo)
            {
                var texec = new SavedTaskExecutor(id, Shards[0].Client, tinfo, SharedData, GlobalDatabaseContextBuilder);
                if (texec.TaskInfo.IsExecutionTimeReached)
                {
                    await texec.HandleMissedExecutionAsync();

                    return false;
                } else
                {
                    texec.Schedule();

                    return true;
                }
            }
        }

        private static async Task DisposeAsync()
        {
            SharedData.LogProvider.ElevatedLog(LogLevel.Info, "Cleaning up...");

            BotStatusUpdateTimer.Dispose();
            DatabaseSyncTimer.Dispose();
            FeedCheckTimer.Dispose();
            MiscActionsTimer.Dispose();

            foreach (var shard in Shards)
                await shard.DisposeAsync();
            SharedData.Dispose();

            SharedData.LogProvider.ElevatedLog(LogLevel.Info, "Cleanup complete! Powering off...");
        }

        #endregion SETUP_FUNCTIONS

        #region PERIODIC_CALLBACKS

        private static void BotActivityCallback(object _)
        {
            if (!SharedData.StatusRotationEnabled)
                return;

            var client = _ as DiscordClient;
            try
            {
                DatabaseBotStatus status;
                using (var dc = GlobalDatabaseContextBuilder.CreateContext())
                    status = dc.BotStatuses.Shuffle().FirstOrDefault();

                var activity = new DiscordActivity(status?.Status ?? "For commands\n@Freud help", status?.Activity ?? ActivityType.ListeningTo);

                SharedData.AsyncExecutor.Execute(client.UpdateStatusAsync(activity));
            } catch (Exception e)
            {
                SharedData.LogProvider.Log(LogLevel.Error, e);
            }
        }

        private static void DatabaseSyncCallback(object _)
        {
            try
            {
                using (var dc = GlobalDatabaseContextBuilder.CreateContext())
                {
                    foreach ((ulong uid, int count) in SharedData.MessageCount)
                    {
                        var msgcount = dc.MessageCount.Find((long)uid);
                        if (msgcount is null)
                        {
                            dc.MessageCount.Add(new DatabaseMessageCount
                            {
                                MessageCount = count,
                                UserId = uid
                            });
                        } else
                        {
                            if (count != msgcount.MessageCount)
                            {
                                msgcount.MessageCount = count;
                                dc.MessageCount.Update(msgcount);
                            }
                        }
                    }

                    dc.SaveChanges();
                }
            } catch (Exception e)
            {
                SharedData.LogProvider.Log(LogLevel.Error, e);
            }
        }

        private static void FeedCheckCallback(object _)
        {
            var client = _ as DiscordClient;
            try
            {
                SharedData.AsyncExecutor.Execute(RssService.CheckFeedsForChangesAsync(client, GlobalDatabaseContextBuilder));
            } catch (Exception e)
            {
                SharedData.LogProvider.Log(LogLevel.Error, e);
            }
        }

        private static void MiscellaneousActionsCallback(object _)
        {
            var client = _ as DiscordClient;
            try
            {
                List<DatabaseBirthday> todayBirthdays;
                using (var dc = GlobalDatabaseContextBuilder.CreateContext())
                {
                    todayBirthdays = dc.Birthdays.Where(b => b.Date.Month == DateTime.Now.Month && b.Date.Day == DateTime.Now.Day && b.LastUpdateYear < DateTime.Now.Year).ToList();
                }

                foreach (DatabaseBirthday birthday in todayBirthdays)
                {
                    var channel = SharedData.AsyncExecutor.Execute(client.GetChannelAsync(birthday.ChannelId));
                    var user = SharedData.AsyncExecutor.Execute(client.GetUserAsync(birthday.UserId));
                    SharedData.AsyncExecutor.Execute(channel.SendMessageAsync(user.Mention, embed: new DiscordEmbedBuilder
                    {
                        Description = $"{StaticDiscordEmoji.Tada} Happy birthday, {user.Mention}! {StaticDiscordEmoji.Cake}",
                        Color = DiscordColor.Aquamarine
                    }));

                    using (var dc = GlobalDatabaseContextBuilder.CreateContext())
                    {
                        birthday.LastUpdateYear = DateTime.Now.Year;
                        dc.Birthdays.Update(birthday);
                        dc.SaveChanges();
                    }
                }

                using (var dc = GlobalDatabaseContextBuilder.CreateContext())
                {
                    dc.Database.ExecuteSqlCommand("UPDATE gf.bank_accounts SET balance = GREATEST(CEILING(1.0015 * balance), 10);");
                    dc.SaveChanges();
                }
            } catch (Exception e)
            {
                SharedData.LogProvider.Log(LogLevel.Error, e);
            }
        }

        #endregion PERIODIC_CALLBACKS
    }
}

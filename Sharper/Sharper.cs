#region USING_DIRECTIVES
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using Sharper.Common.Attributes;
using Sharper.Common.Configuration;
using Sharper.Database;
#endregion

namespace Sharper
{
    internal static class Sharper
    {
        public static readonly string ApplicationName = "Sharper";
        public static readonly string ApplicationVersion = "v1";
        public static IReadOnlyList<SharperShard> ActiveShards => ActiveShards.AsReadOnly();

        private static BotConfiguration BotConfiguration { get; set; }
        private static DatabaseContextBuilder GlobalDatabaseContextBuilder { get; set; }
        private static List<SharperShard> Shards { get; set; }
        private static SharedData SharedData { get; set; }

        #region TIMERS
        private static Timer BotStatusUpdateTimer { get; set; }
        private static Timer DatabaseSyncTimer { get; set; }
        private static Timer FeedCheckTimer { get; set; }
        private static Timer MiscActionsTimer { get; set; }
        #endregion

        internal static async Task Main(string[] _)
        {
            try
            {
                PrintBuildInformation();

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                await LoadBotConfigurationAsync();
                await InitializedDatabaseAsync();
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

        #region SETUP_FUNCTIONS
        //todo:
        #endregion

        #region PERIODIC_CALLBACKS
        private static void BotActivityCallback(object _)
        {
            if (!SharedData.StatusRotationEnabled)
                return;

            var client = _ as DiscordClient;
            try
            {
                DatabaseBotStatus status;
                using (DatabaseContext db = GlobalDatabaseContextBuilder.CreateContext())
                    status = db.BotStatuses.Shuffle().FirstOrDefault();

                var activity = new DiscordActivity(status?.Status ?? "For commands\n@Freud help", status?.Activity ?? ActivityType.Listening);

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
                using (DatabaseContext db = GlobalDatabaseContextBuilder.CreateContext())
                {
                    foreach ((ulong uid, int count) in SharedData.MessageCount)
                    {
                        DatabaseMessageCount msgcount = db.MessageCount.Find((long)uid);
                        if (msgcount is null)
                        {
                            db.MessageCount.Add(new DatabaseMessageCount
                            {
                                MessageCount = count,
                                UserId = uid
                            });
                        } else
                        {
                            if (count != msgcount.MessageCount)
                            {
                                msgcount.MessageCount = count;
                                db.MessageCount.Update(msgcount);
                            }
                        }
                    }

                    db.SaveChanges();
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

            } catch (Exception e)
            {
                SharedData.LogProvider.Log(LogLevel.Error, e);
            }
        }
        #endregion
    }
}

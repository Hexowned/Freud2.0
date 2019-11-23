﻿#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using Sharper.Common.Configuration;
using Sharper.Database;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Sharper.Common.Tasks
{
    public sealed class SavedTaskExecutor : AsyncExecutor, IDisposable
    {
        public int Id { get; private set; }
        public SavedTaskInfo TaskInfo { get; }
        private readonly DiscordClient client;
        private readonly SharedData shared;
        private readonly DatabaseContextBuilder dcb;
        private Timer timer;

        public static async Task ScheduleAsync(SharedData shared, DatabaseContextBuilder dcb, DiscordClient client, SavedTaskInfo task)
        {
            SavedTaskExecutor texec = null;
            try
            {
                using (DatabaseContext dc = dcb.CreateContext())
                {
                    if (task is SendMessageTaskInfo)
                    {
                        var dbtask = DatabaseReminder.FromSavedTaskInfo(task);
                        dc.Reminders.Add(dbtask);
                        await dc.SaveChangesAsync();
                        texec = new SavedTaskExecutor(dbtask.Id, client, task, shared, dcb);
                    }
                    else
                    {
                        var dbtask = DatabaseSavedTask.FromSavedTaskInfo(task);
                        dc.SavedTask.Add(dbtask);
                        await dc.SaveChangesAsync();
                        texec = new SavedTaskExecutor(dbtask.Id, client, task, shared, dcb);
                    }
                }

                texec.Schedule();
            }
            catch (Exception e)
            {
                await texec?.UnscheduleAsync();
                shared.LogProvider.Log(LogLevel.Warning, e);
                throw;
            }
        }

        public static Task UnscheduleAsync(SharedData shared, ulong uid, int id)
        {
            if (shared.RemindExecuters.TryGetValue(uid, out ConcurrentDictionary<int, SavedTaskExecutor> texecs))
                return texecs.TryGetValue(id, out SavedTaskExecutor texec) ? texec.UnscheduleAsync() : Task.CompletedTask;
            else
                return Task.CompletedTask;
        }

        public SavedTaskExecutor(int id, DiscordClient client, SavedTaskInfo task, SharedData data, DatabaseContextBuilder dcb)
        {
            this.Id = id;
            this.client = client;
            this.TaskInfo = task;
            this.shared = data;
            this.dcb = dcb;
        }

        public void Dispose()
            => this.timer?.Dispose();

        public void Schedule()
        {
            switch (this.TaskInfo)
            {
                case SendMessageTaskInfo smti:
                    this.timer = new Timer(this.SendMessageCallback, this.TaskInfo, smti.TimeUntilExecution, smti.RepeatingInterval);
                    if (!this.shared.RemindExecuters.TryGetValue(smti.InitiatorId, out ConcurrentDictionary<int, SavedTaskExecutor> texecs))
                        texecs = new ConcurrentDictionary<int, SavedTaskExecutor>();
                    if (!texecs.TryAdd(this.Id, this))
                        throw new ConcurrentOperationException("Failed to add reminder!");
                    this.shared.RemindExecuters.AddOrUpdate(smti.InitiatorId, texecs, (k, var) => texecs);
                    break;

                case UnbanTaskInfo _:
                    this.timer = new Timer(this.UnbanUserCallback, this.TaskInfo, this.TaskInfo.TimeUntilExecution, TimeSpan.FromMilliseconds(-1));
                    if (!this.shared.TaskExecuters.TryAdd(this.Id, this))
                        throw new ConcurrentOperationException("Failed to schedule the task.");
                    break;

                case UnmuteTaskInfo _:
                    this.timer = new Timer(this.UnmuteUserCallback, this.TaskInfo, this.TaskInfo.TimeUntilExecution, TimeSpan.FromMilliseconds(-1));
                    if (!this.shared.TaskExecuters.TryAdd(this.Id, this))
                        throw new ConcurrentOperationException("Failed to schedule the task.");
                    break;

                default:
                    throw new ArgumentException("Unknown saved task info type!", nameof(this.TaskInfo));
            }
        }

        public async Task HandleMissedExecutionAsync()
        {
            bool unschedule = true;
            try
            {
                switch (this.TaskInfo)
                {
                    case SendMessageTaskInfo smti:
                        DiscordChannel channel;
                        if (smti.ChannelId != 0)
                            channel = await this.client.GetChannelAsync(smti.ChannelId);
                        else
                            channel = await this.client.CreateDmChannelAsync(smti.InitiatorId);
                        DiscordUser user = await this.client.GetUserAsync(smti.InitiatorId);
                        await channel?.SendMessageAsync($"{user.Mention}'s reminder:", embed: new DiscordEmbedBuilder
                        {
                            Description = $"{StaticDiscordEmoji.X} I have been asleep and failed to remind {user.Mention} to:\n\n{smti.Message}\n\n{smti.ExecutionTime.ToUtcTimestamp()}",
                            Color = DiscordColor.Red
                        });
                        break;

                    case UnbanTaskInfo _:
                        this.UnbanUserCallback(this.TaskInfo);
                        break;

                    case UnmuteTaskInfo _:
                        this.UnmuteUserCallback(this.TaskInfo);
                        break;
                }

                this.shared.LogProvider.Log(LogLevel.Debug, $"Executed missed task: {this.TaskInfo.GetType().ToString()}");
            }
            catch (Exception e)
            {
                this.shared.LogProvider.Log(LogLevel.Debug, e);
            }
            finally
            {
                try
                {
                    if (unschedule)
                        await this.UnscheduleAsync();
                }
                catch (Exception e)
                {
                    this.shared.LogProvider.Log(LogLevel.Debug, e);
                }
            }
        }

        private async Task UnscheduleAsync()
        {
            this.Dispose();
            switch (this.TaskInfo)
            {
                case SendMessageTaskInfo smti:
                    Exception ex = null;
                    if (this.shared.RemindExecuters.TryGetValue(smti.InitiatorId, out ConcurrentDictionary<int, SavedTaskExecutor> texecs))
                    {
                        if (!texecs.TryRemove(this.Id, out _))
                            ex = new ConcurrentOperationException("Failed to remove reminder from the dictionary!");
                        if (texecs.Count == 0)
                            this.shared.RemindExecuters.TryRemove(smti.InitiatorId, out ConcurrentDictionary<int, SavedTaskExecutor> _);
                    }
                    using (DatabaseContext dc = this.dcb.CreateContext())
                    {
                        dc.Reminders.Remove(new DatabaseReminder { Id = this.Id });
                        await dc.SaveChangesAsync();
                    }
                    if (!(ex is null))
                        throw ex;
                    break;

                case UnbanTaskInfo _:
                case UnmuteTaskInfo _:
                    this.shared.TaskExecuters.TryRemove(this.Id, out SavedTaskExecutor _);
                    using (DatabaseContext dc = this.dcb.CreateContext())
                    {
                        dc.SavedTasks.Remove(new DatabaseSavedTask { Id = this.Id });
                        await dc.SaveChangesAsync();
                    }
                    break;

                default:
                    throw new ArgumentException("Unknown saved task info type!", nameof(this.TaskInfo));
            }
        }

        #region CALLBACKS

        private void SendMessageCallback(object _)
        {
            var info = _ as SendMessageTaskInfo;
            try
            {
                DiscordChannel channel;
                if (info.ChannelId != 0)
                {
                    channel = this.Execute(this.client.GetChannelAsync(info.ChannelId));
                    DiscordMember member = this.Execute(channel.Guild.GetMemberAsync(info.InitiatorId));
                    if (!(member is null) && channel.PermissionsFor(member).HasPermission(Permissions.AccessChannels | Permissions.SendMessages))
                        SendMessage(member);
                }
                else
                {
                    channel = this.Execute(this.client.CreateDmChannelAsync(info.InitiatorId));
                    DiscordUser user = this.Execute(this.client.GetUserAsync(info.InitiatorId));
                    SendMessage(user);
                }

                DiscordMessage SendMessage(DiscordUser user)
                {
                    if (user is null)
                        return null;
                    return this.Execute(channel.SendMessageAsync($"{user.Mention}'s reminder:", embed: new DiscordEmbedBuilder
                    {
                        Description = $"{StaticDiscordEmoji.AlarmClock} {info.Message}",
                        Color = DiscordColor.Orange
                    }));
                }
            }
            catch (UnauthorizedException)
            { // Do nothing, user has disabled DM
            }
            catch (Exception e)
            {
                this.shared.LogProvider.Log(LogLevel.Warning, e);
            }
            finally
            {
                if (!info.IsRepeating)
                {
                    try
                    {
                        this.Execute(this.UnscheduleAsync());
                    }
                    catch (Exception e)
                    {
                        this.shared.LogProvider.Log(LogLevel.Error, e);
                    }
                }
            }
        }

        private void UnbanUserCallback(object _)
        {
            var info = _ as UnbanTaskInfo;
            try
            {
                DiscordGuild guild = this.Execute(this.client.GetGuildAsync(info.GuildId));
                this.Execute(guild.UnbanMemberAsync(info.UnbanId, $"Temporary ban time expired"));
            }
            catch (UnauthorizedException)
            { // Do nothing, perms to unban removed
            }
            catch (Exception e)
            {
                this.shared.LogProvider.Log(LogLevel.Warning, e);
            }
            finally
            {
                try
                {
                    this.Execute(this.UnscheduleAsync());
                }
                catch (Exception e)
                {
                    this.shared.LogProvider.Log(LogLevel.Error, e);
                }
            }
        }

        private void UnmuteUserCallback(object _)
        {
            var info = _ as UnmuteTaskInfo;
            try
            {
                DiscordGuild guild = this.Execute(this.client.GetGuildAsync(info.GuildId));
                DiscordRole role = guild.GetRole(info.MuteRoleId);
                DiscordMember member = this.Execute(guild.GetMemberAsync(info.UserId));
                if (role is null)
                    return;
                this.Execute(member.RevokeRoleAsync(role, $"Temporary mute time expired"));
            }
            catch (UnauthorizedException)
            { // Do nothing, perms to unmute removed
            }
            catch (Exception e)
            {
                this.shared.LogProvider.Log(LogLevel.Warning, e);
            }
            finally
            {
                try
                {
                    this.Execute(this.UnscheduleAsync());
                }
                catch (Exception e)
                {
                    this.shared.LogProvider.Log(LogLevel.Error, e);
                }
            }
        }

        #endregion CALLBACKS
    }
}
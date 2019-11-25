#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.Entities;
using Freud.Common.Configuration;
using Freud.Common.Tasks;
using Freud.Discord.Extensions;
using Freud.Modules.Administration.Common;
using Freud.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Administration.Services
{
    public abstract class ProtectionService : IFreudService
    {
        protected FreudShard shard;
        protected SemaphoreSlim csem = new SemaphoreSlim(1, 1);
        protected string reason;

        public abstract bool TryAddGuildToWatch(ulong gid);

        public abstract bool TryRemoveGuildFromWatch(ulong gid);

        protected ProtectionService(FreudShard shard)
        {
            this.shard = shard;
        }

        public bool IsDisabled()
            => false;

        public async Task PunishMemberAsync(DiscordGuild guild, DiscordMember member, PunishmentActionType type, TimeSpan? cooldown = null, string reason = null)
        {
            try
            {
                DiscordRole muteRole;
                SavedTaskInfo task;
                switch (type)
                {
                    case PunishmentActionType.Kick:
                        await member.RemoveAsync(reason ?? this.reason);
                        break;

                    case PunishmentActionType.PermanentMute:
                        muteRole = await this.GetOrCreateMuteRoleAsync(guild);
                        if (member.Roles.Contains(muteRole))
                            return;
                        await member.GrantRoleAsync(muteRole, reason ?? this.reason);
                        break;

                    case PunishmentActionType.PermanentBan:
                        await member.BanAsync(1, reason: reason ?? this.reason);
                        break;

                    case PunishmentActionType.TemporaryBan:
                        await member.BanAsync(0, reason: reason ?? this.reason);
                        task = new UnbanTaskInfo(guild.Id, member.Id, cooldown is null ? null : DateTimeOffset.Now + cooldown);
                        await SavedTaskExecutor.ScheduleAsync(this.shard.SharedData, this.shard.Database, this.shard.Client, task);
                        break;

                    case PunishmentActionType.TemporaryMute:
                        muteRole = await this.GetOrCreateMuteRoleAsync(guild);
                        if (member.Roles.Contains(muteRole))
                            return;
                        await member.GrantRoleAsync(muteRole, reason ?? this.reason);
                        task = new UnmuteTaskInfo(guild.Id, member.Id, muteRole.Id, cooldown is null ? null : DateTimeOffset.Now + cooldown);
                        await SavedTaskExecutor.ScheduleAsync(this.shard.SharedData, this.shard.Database, this.shard.Client, task);
                        break;
                }
            } catch
            {
                var logchn = this.shard.SharedData.GetLogChannelForGuild(this.shard.Client, guild);
                if (!(logchn is null))
                {
                    var emb = new DiscordEmbedBuilder
                    {
                        Title = "User punish attemp failed! Check my permissions...",
                        Color = DiscordColor.Red
                    };
                    emb.AddField("User", member?.ToString() ?? "unknown", inline: true);
                    emb.AddField("Reason", reason ?? this.reason, inline: false);

                    await logchn.SendMessageAsync(embed: emb.Build());
                }
            }
        }

        public async Task<DiscordRole> GetOrCreateMuteRoleAsync(DiscordGuild guild)
        {
            DiscordRole muteRole = null;

            await this.csem.WaitAsync();
            try
            {
                using (var dc = this.shard.Database.CreateContext())
                {
                    DatabaseGuildConfiguration gcfg = guild.GetGuildConfiguration(dc);
                    muteRole = guild.GetRole(gcfg.MuteRoleId);
                    if (muteRole is null)
                        muteRole = guild.Roles.Values.FirstOrDefault(r => r.Name.ToLowerInvariant() == "f_mute");
                    if (muteRole is null)
                    {
                        muteRole = await guild.CreateRoleAsync("f_mute", hoist: false, mentionable: false);
                        foreach (var channel in guild.Channels.Values.Where(c => c.Type == ChannelType.Text))
                        {
                            await channel.AddOverwriteAsync(muteRole, deny: Permissions.SendMessages | Permissions.SendTtsMessages | Permissions.AddReactions);
                            await Task.Delay(100);
                        }
                        gcfg.MuteRoleId = muteRole.Id;
                        dc.GuildConfiguration.Update(gcfg);
                        await dc.SaveChangesAsync();
                    }
                }
            } finally { this.csem.Release(); }

            return muteRole;
        }
    }
}

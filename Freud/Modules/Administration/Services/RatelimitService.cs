#region USING_DIRECTIVES

using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Freud.Common.Collections;
using Freud.Exceptions;
using Freud.Modules.Administration.Common;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Administration.Services
{
    public sealed class RatelimitService : ProtectionService
    {
        private readonly ConcurrentDictionary<ulong, ConcurrentHashSet<ExemptedEntity>> guildExempts;
        private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, UserRatelimitInfo>> guildRatelimitInfo;
        private readonly Timer refreshTimer;

        private static void RefreshCallback(object _)
        {
            var service = _ as RatelimitService;

            foreach (ulong gid in service.guildRatelimitInfo.Keys)
            {
                var toRemove = service.guildRatelimitInfo[gid].Where(kvp => !kvp.Value.IsActive).Select(kvp => kvp.Key);

                foreach (ulong uid in toRemove)
                    service.guildRatelimitInfo[gid].TryRemove(uid, out var _);
            }
        }

        public RatelimitService(FreudShard shard)
            : base(shard)
        {
            this.guildExempts = new ConcurrentDictionary<ulong, ConcurrentHashSet<ExemptedEntity>>();
            this.guildRatelimitInfo = new ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, UserRatelimitInfo>>();
            this.refreshTimer = new Timer(RefreshCallback, this, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));
            this.reason = "bot: Ratelimit hit";
        }

        public override bool TryAddGuildToWatch(ulong gid)
            => this.guildRatelimitInfo.TryAdd(gid, new ConcurrentDictionary<ulong, UserRatelimitInfo>());

        public override bool TryRemoveGuildFromWatch(ulong gid)
            => this.guildRatelimitInfo.TryAdd(gid, new ConcurrentDictionary<ulong, UserRatelimitInfo>());

        public void UpdateExemptsForGuildAsync(ulong gid)
        {
            using (var dc = this.shard.Database.CreateContext())
            {
                this.guildExempts[gid] = new ConcurrentHashSet<ExemptedEntity>(dc.RatelimitExempts.Where(ee => ee.GuildId == gid).Select(ee => new ExemptedEntity { GuildId = ee.GuildId, Id = ee.Id, Type = ee.Type }));
            }
        }

        public async Task HandleNewMessageAsync(MessageCreateEventArgs e, RatelimitSettings settings)
        {
            if (!this.guildRatelimitInfo.ContainsKey(e.Guild.Id))
            {
                if (!this.TryAddGuildToWatch(e.Guild.Id))
                    throw new ConcurrentOperationException("Failed to add guild to ratelimit watch list!");
                this.UpdateExemptsForGuildAsync(e.Guild.Id);
            }

            var member = e.Author as DiscordMember;
            if (this.guildExempts.TryGetValue(e.Guild.Id, out var exempts))
            {
                if (exempts.Any(ee => ee.Type == ExemptedEntityType.Channel && ee.Id == e.Channel.Id))
                    return;
                if (exempts.Any(ee => ee.Type == ExemptedEntityType.Member && ee.Id == e.Author.Id))
                    return;
                if (exempts.Any(ee => ee.Type == ExemptedEntityType.Role && member.Roles.Any(r => r.Id == ee.Id)))
                    return;
            }

            var gRateInfo = this.guildRatelimitInfo[e.Guild.Id];
            if (!gRateInfo.ContainsKey(e.Author.Id))
            {
                if (!gRateInfo.TryAdd(e.Author.Id, new UserRatelimitInfo(settings.Sensitivity)))
                    throw new ConcurrentOperationException("Failed to add member to ratelimit watch list!");
                return;
            }

            if (gRateInfo.TryGetValue(e.Author.Id, out var rateInfo) && !rateInfo.TryDecrementAllowedMessageCount())
            {
                await this.PunishMemberAsync(e.Guild, member, settings.Action);
                rateInfo.Reset();
            }
        }
    }
}

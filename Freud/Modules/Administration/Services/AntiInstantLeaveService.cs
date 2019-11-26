#region USING_DIRECTIVES

using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Freud.Common.Collections;
using Freud.Exceptions;
using Freud.Modules.Administration.Common;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Administration.Services
{
    public class AntiInstantLeaveService : ProtectionService
    {
        private readonly ConcurrentDictionary<ulong, ConcurrentHashSet<DiscordMember>> newGuildMembers;

        public AntiInstantLeaveService(FreudShard shard)
            : base(shard)
        {
            this.newGuildMembers = new ConcurrentDictionary<ulong, ConcurrentHashSet<DiscordMember>>();
            this.reason = "bot: Instant leave";
        }

        public override bool TryAddGuildToWatch(ulong gid)
            => this.newGuildMembers.TryAdd(gid, new ConcurrentHashSet<DiscordMember>());

        public override bool TryRemoveGuildFromWatch(ulong gid)
            => this.newGuildMembers.TryRemove(gid, out _);

        public async Task HandleMemberJoinAsync(GuildMemberAddEventArgs e, AntiInstantLeaveSettings settings)
        {
            if (!this.newGuildMembers.ContainsKey(e.Guild.Id) && !this.TryAddGuildToWatch(e.Guild.Id))
                throw new ConcurrentOperationException("Failed to add guild to instant-leave watch list...!");

            if (!this.newGuildMembers[e.Guild.Id].Add(e.Member))
                throw new ConcurrentOperationException("Faled to add member to instant-leave watch list...!");

            await Task.Delay(TimeSpan.FromSeconds(settings.Cooldown));

            if (this.newGuildMembers.ContainsKey(e.Guild.Id) && !this.newGuildMembers[e.Guild.Id].TryRemove(e.Member))
                throw new ConcurrentOperationException("Failed to remove member from instant-leave watch list...!");
        }

        public async Task<bool> HandleMemberLeaveAsync(GuildMemberRemoveEventArgs e, AntiInstantLeaveSettings settings)
        {
            if (!this.newGuildMembers.ContainsKey(e.Guild.Id) || !this.newGuildMembers[e.Guild.Id].Contains(e.Member))
                return false;

            await this.PunishMemberAsync(e.Guild, e.Member, PunishmentActionType.PermanentBan);

            return true;
        }
    }
}

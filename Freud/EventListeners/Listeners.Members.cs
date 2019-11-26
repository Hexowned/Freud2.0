#region USING DIRECTIVES

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using Freud.Common;
using Freud.Common.Attributes;
using Freud.Common.Configuration;
using Freud.Discord.Extensions;
using Freud.Extensions;
using Freud.Extensions.Discord;
using Freud.Modules.Administration.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#endregion USING DIRECTIVES

namespace Freud.EventListeners
{
    internal static partial class Listeners
    {
        [AsyncEventListener(DiscordEventType.GuildMemberAdded)]
        public static async Task MemberJoinEventHandlerAsync(FreudShard shard, GuildMemberAddEventArgs e)
        {
            var gcfg = e.Guild.GetGuildSettings(shard.Database);
            await Task.Delay(TimeSpan.FromSeconds(gcfg.AntiInstantLeaveSettings.Cooldown + 1));
            if (e.Member.Guild is null)
                return;

            var whcn = e.Guild.GetChannel(gcfg.WelcomeChannelId);
            if (!(whcn is null))
            {
                if (string.IsNullOrWhiteSpace(gcfg.WelcomeMessage))
                    await whcn.EmbedAsync($"Welcome to {Formatter.Bold(e.Guild.Name)}, {e.Member.Mention}!", StaticDiscordEmoji.Wave);
                else
                    await whcn.EmbedAsync($"Welcome to {Formatter.Bold(e.Guild.Name)}, {e.Member.Mention}!", StaticDiscordEmoji.Wave);
            }
            try
            {
                using (var dc = shard.Database.CreateContext())
                {
                    IQueryable<ulong> rids = dc.AutoAssinableRoles.Where(dbr => dbr.RoleId);
                    foreach (ulong rid in rids.ToList())
                    {
                        try
                        {
                            var role = e.Guild.GetRole(rid);
                            if (!(role is null))
                                await e.Member.GrantRoleAsync(role);
                            else
                                dc.AutoAssignableRoles.Remove(dc.AutoAssinableRoles.Single(r => r.GuildId == e.Guild.Id && r.RoleId == rid));
                        } catch (Exception exc)
                        {
                            shard.Log(LogLevel.Debug,
                                $"| Failed to assign an automatic role to a new member!\n" +
                                $"| {e.Guild.ToString()}\n" +
                                $"| Exception: {exc.GetType()}" +
                                $"| Message: {exc.Message}"
                            );
                        }
                    }
                }
            } catch (Exception exc)
            {
                shard.SharedData.LogProvider.Log(LogLevel.Debug, exc);
            }

            var logchn = shard.SharedData.GetLogChannelForGuild(shard.Client, e.Guild);
            if (logchn is null)
                return;

            var emb = FormEmbedBuilder(EventOrigin.Member, "Member joined", e.Member.ToString());
            emb.WithThumbnailUrl(e.Member.AvatarUrl);
            emb.AddField("Registration time", e.Member.CreationTimestamp.ToUtcTimestamp(), inline: true);
            if (!string.IsNullOrWhiteSpace(e.Member.Email))
                emb.AddField("Email", e.Member.Email);

            using (var dc = shard.Database.CreateContext())
            {
                if (dc.ForbiddenNames.Any(n => n.GuildId == e.Guild.Id && n.Regex.IsMatch(e.Member.DisplayName)))
                {
                    try
                    {
                        await e.Member.ModifyAsync(m =>
                        {
                            m.Nickname = "Temporary name";
                            m.AuditLogReason = "_gf: Forbidden name match";
                        });
                        emb.AddField("Additional actions taken", "Removed name due to a match with a foribidden name");
                        if (!e.Member.IsBot)
                            await e.Member.SendMessageAsync($"Your nickname in the guild {e.Guild.Name} is forbidden by I. Please set a different name. ");
                    } catch (UnauthorizedException)
                    {
                        emb.AddField("Additional actions taken", "Matched forbidden name, but I failed to remove it. Check my permissions");
                    }
                }
            }

            await logchn.SendMessageAsync(embed: emb.Build());
        }

        [AsyncEventListener(DiscordEventType.GuildMemberAdded)]
        public static async Task MemberJoinProtectionEventHandlerAsync(FreudShard shard, GuildMemberAddEventArgs e)
        {
            if (e.Member is null || e.Member.IsBot)
                return;

            var gcfg = e.Guild.GetGuildSettings(shard.Database);
            if (gcfg.AntifloodEnabled)
                await shard.CNext.Services.GetService<AntifloodService>().HandlerMemberJoinAsync(e, gcfg.AntifloodSettings);

            if (gcfg.AntiInstantLeaveEnabled)
                await shard.CNext.Services.GetService<AntiInstantLeaveService>().HandleMemerJoinAsync(e, gcfg.AntiInstantLeaveSettings);
        }

        [AsyncEventListener(DiscordEventType.GuildMemberRemoved)]
        public static async Task MemberRemoveEventHandlerAsync(FreudShard shard, GuildMemberRemoveEventArgs e)
        {
            if (e.Member.IsCurrent)
                return;

            var gcfg = e.Guild.GetGuildSettings(shard.Database);
            bool punished = false;

            if (gcfg.AntiInstantLeaveEnabled)
                punished = await shard.CNext.Services.GetService<AntiInstantLeaveService>().HandleMemberLeaveAsync(e, gcfg.AntiInstantLeaveSettings);
            if (!punished)
            {
                var lchn = e.Guild.GetChannel(gcfg.LeaveChannelId);
                if (!(lchn is null))
                {
                    if (string.IsNullOrWhiteSpace(gcfg.LeaveMessage))
                        await lchn.EmbedAsync($"{Formatter.Bold(e.Member?.Username ?? _unknown)} bailed from the channel", StaticDiscordEmoji.Wave);
                    else
                        await lchn.EmbedAsync(gcfg.LeaveMessage.Replace("%user%", e.Member?.Username ?? _unknown), StaticDiscordEmoji.Wave);
                }
            }

            var logchn = shard.SharedData.GetLogChannelForGuild(shard.Client, e.Guild);
            if (logchn is null)
                return;

            var emb = FormEmbedBuilder(EventOrigin.Member, "Member left", e.Member.ToString());
            var kickEntry = await e.Guild.GetLatestAuditLogEntryAsync(AuditLogActionType.Kick);
            if (!(kickEntry is null) && kickEntry is DiscordAuditLogKickEntry ke && ke.Target.Id == e.Member.Id)
            {
                emb.WithTitle("Member kicked");
                emb.AddField("User responsible", ke.UserResponsible.Mention);
                emb.AddField("Reason", ke.Reason ?? "No reason provided.");
            }

            emb.WithThumbnailUrl(e.Member.AvatarUrl);
            emb.AddField("Registration time", e.Member.CreationTimestamp.ToUtcTimestamp(), inline: true);
            if (!string.IsNullOrWhiteSpace(e.Member.Email))
                emb.AddField("Email", e.Member.Email);

            await logchn.SendMessageAsync(embed: emb.Build());
        }

        [AsyncEventListener(DiscordEventType.GuildMemberUpdated)]
        public static async Task MemberUpdateEventHandlerAsync(FreudShard shard, GuildMemberUpdateEventArgs e)
        {
            var logchn = shard.SharedData.GetLogChannelForGuild(shard.Client, e.Guild);
            if (logchn is null)
                return;

            var emb = FormEmbedBuilder(EventOrigin.Member, "Member updated", e.Member.ToString());
            emb.WithThumbnailUrl(e.Member.AvatarUrl);

            DiscordAuditLogEntry entry = null;
            if (e.RolesBefore.Count == e.RolesAfter.Count)
                entry = await e.Guild.GetLatestAuditLogEntryAsync(AuditLogActionType.MemberUpdate);
            else
                entry = await e.Guild.GetLatestAuditLogEntryAsync(AuditLogActionType.MemberRoleUpdate);

            if (!(entry is null) && entry is DiscordAuditLogMemberUpdateEntry mentry)
            {
                emb.AddField("User responsible", mentry.UserResponsible.Mention, inline: true);
                if (!(mentry.NicknameChange is null))
                    emb.AddField("Nickname change", $"{mentry.NicknameChange.Before} -> {mentry.NicknameChange.After}", inline: true);

                if (!(mentry.AddedRoles is null))
                    emb.AddField("Added roles", string.Join(",", mentry.AddedRoles.Select(r => r.Name)), inline: true);

                if (!(mentry.RemovedRoles is null))
                    emb.AddField("Removed roles", string.Join(",", mentry.RemovedRoles.Select(r => r.Name)), inline: true);

                if (!string.IsNullOrWhiteSpace(mentry.Reason))
                    emb.AddField("Reason", mentry.Reason);
                emb.WithFooter(mentry.CreationTimestamp.ToUtcTimestamp(), mentry.UserResponsible.AvatarUrl);
            } else
            {
                emb.AddField("Error", "Failed to read audit log information. Please check my permissions");
                emb.AddField("Name before", e.NicknameBefore ?? _unknown, inline: true);
                emb.AddField("Name after", e.NicknameAfter ?? _unknown, inline: true);
                emb.AddField("Role before", e.RolesBefore.Count.ToString() ?? _unknown, inline: true);
                emb.AddField("Role after", e.RolesAfter?.Count.ToString() ?? _unknown, inline: true);
            }

            using (var dc = shard.Database.CreateContext())
            {
                if (!string.IsNullOrWhiteSpace(e.NicknameAfter) && dc.ForbiddenNames.Any(n => n.GuildId == e.Guild.Id && n.Regex.IsMatch(e.NicknameAfter)))
                {
                    try
                    {
                        await e.Member.ModifyAsync(m =>
                        {
                            m.Nickname = e.NicknameBefore;
                            m.AuditLogReason = "_f: Forbidden name match";
                        });
                        emb.AddField("Additional actions taken", "Removed name due to a match with a forbidden name");
                        if (!e.Member.IsBot)
                            await e.Member.SendMessageAsync($"The nickname you tried to set in the guild {e.Guild.Name} is forbidden by the guild administrator. Please set a different name.");
                    } catch (UnauthorizedException)
                    {
                        emb.AddField("Additional actions taken", "Matched forbidden name, but I failed to remove it. Check my permissions");
                    }
                }
            }

            await logchn.SendMessageAsync(embed: emb.Build());
        }

        [AsyncEventListener(DiscordEventType.PresenceUpdated)]
        public static async Task MemberPresenceUpdateEventHandlerAsync(FreudShard shard, PresenceUpdateEventArgs e)
        {
            if (e.User.IsBot)
                return;

            var emb = FormEmbedBuilder(EventOrigin.Member, "User updated", e.User.ToString());
            emb.WithTitle("User Updated");
            emb.WithDescription(e.UserAfter.ToString());
            if (e.UserAfter.Username != e.UserBefore.Username)
                emb.AddField("Changed discriminator", $"{e.UserBefore.Discriminator} to {e.UserAfter.Discriminator}");

            if (e.UserAfter.AvatarUrl != e.UserBefore.AvatarUrl)
                emb.AddField("Changed avatar", Formatter.MaskedUrl("Old Avatar (note: 404 possible)", new Uri(e.UserBefore.AvatarUrl)));

            emb.WithThumbnailUrl(e.UserAfter.AvatarUrl);

            if (!emb.Fields.Any())
                return;

            var guilds = Freud.ActiveShards.SelectMany(s => s.Client?.Guilds).Select(kvp => kvp.Value) ?? Enumerable.Empty<DiscordGuild>();

            foreach (var guild in guilds)
            {
                var logchn = shard.SharedData.GetLogChannelForGuild(shard.Client, guild);
                if (logchn is null)
                    continue;
                if (await e.UserAfter.IsMemberOfGuildAsync(guild))

                    await logchn.SendMessageAsync(embed: emb.Build());
            }
        }
    }
}

﻿#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Freud.Common.Attributes;
using Freud.Database.Db;
using Freud.Database.Db.Entities;
using Freud.Exceptions;
using Freud.Extensions.Discord;
using Freud.Modules.Administration.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Administration
{
    [Group("selfassignableroles"), Module(ModuleType.Administration), NotBlocked]
    [Description("Self-assignable roles management. A member can grant himself a self-assignable role" +
                "using ``giveme`` command. Group call lists all self-assignable roles for the guild. " +
                "Group call with an arbitrary amount of roles will add those roles to the self-assignable " +
                "roles list for this guild, effective immediately.")]
    [Aliases("sar", "selfroles", "selfrole")]
    [UsageExampleArgs("@Announcements")]
    [RequireUserPermissions(Permissions.Administrator)]
    [Cooldown(3, 5, CooldownBucketType.Guild)]
    public class SelfAssignableRolesModule : FreudModule
    {
        public SelfAssignableRolesModule(SharedData shared, DatabaseContextBuilder dcb)
            : base(shared, dcb)
        {
            this.ModuleColor = DiscordColor.Goldenrod;
        }

        [GroupCommand, Priority(1)]
        public Task ExecuteGroupAsync(CommandContext ctx)
           => this.ListAsync(ctx);

        [GroupCommand, Priority(0)]
        public Task ExecuteGroupAsync(CommandContext ctx,
                                     [Description("Roles to add.")] params DiscordRole[] roles)
            => this.AddAsync(ctx, roles);

        #region COMMAND_SAR_ADD

        [Command("add")]
        [Description("Add a self-assignable role(s).")]
        [Aliases("a", "+", "+=", "<<", "<")]
        [UsageExampleArgs("@Notifications", "@Role1 @Role2 ...")]
        public async Task AddAsync(CommandContext ctx,
                                  [Description("Roles to add.")] params DiscordRole[] roles)
        {
            if (roles is null || !roles.Any())
                throw new InvalidCommandUsageException("Missing roles to add.");

            using (var dc = this.Database.CreateContext())
            {
                dc.SelfAssignableRoles.SafeAddRange(roles.Select(r => new DatabaseSelfRole
                {
                    RoleId = r.Id,
                    GuildId = ctx.Guild.Id
                }));
                await dc.SaveChangesAsync();
            }

            var logchn = this.Shared.GetLogChannelForGuild(ctx.Client, ctx.Guild);
            if (!(logchn is null))
            {
                var emb = new DiscordEmbedBuilder
                {
                    Title = "Self-assignable roles change occured",
                    Color = this.ModuleColor
                };
                emb.AddField("User responsible", ctx.User.Mention, inline: true);
                emb.AddField("Invoked in", ctx.Channel.Mention, inline: true);
                emb.AddField("Roles added", string.Join("\n", roles.Select(r => r.ToString())));
                await logchn.SendMessageAsync(embed: emb.Build());
            }

            await this.InformAsync(ctx, $"Added self-assignable roles:\n\n{string.Join("\n", roles.Select(r => r.ToString()))}", important: false);
        }

        #endregion COMMAND_SAR_ADD

        #region COMMAND_SAR_DELETE

        [Command("delete")]
        [Description("Remove self-assignable role(s).")]
        [Aliases("remove", "rm", "del", "d", "-", "-=", ">", ">>")]
        [UsageExampleArgs("@Notifications", "@Role1 @Role2 ...")]
        public async Task DeleteAsync(CommandContext ctx,
                                     [Description("Roles to remove.")] params DiscordRole[] roles)
        {
            if (roles is null || !roles.Any())
                throw new InvalidCommandUsageException("You need to specify roles to remove.");

            using (var dc = this.Database.CreateContext())
            {
                dc.SelfAssignableRoles.RemoveRange(dc.SelfAssignableRoles.Where(sar => sar.GuildId == ctx.Guild.Id && roles.Any(r => r.Id == sar.RoleId)));
                await dc.SaveChangesAsync();
            }

            var logchn = this.Shared.GetLogChannelForGuild(ctx.Client, ctx.Guild);
            if (!(logchn is null))
            {
                var emb = new DiscordEmbedBuilder
                {
                    Title = "Self-assignable roles change occured",
                    Color = this.ModuleColor
                };
                emb.AddField("User responsible", ctx.User.Mention, inline: true);
                emb.AddField("Invoked in", ctx.Channel.Mention, inline: true);
                emb.AddField("Roles removed", string.Join("\n", roles.Select(r => r.ToString())));
                await logchn.SendMessageAsync(embed: emb.Build());
            }

            await this.InformAsync(ctx, $"Removed self-assignable roles:\n\n{string.Join("\n", roles.Select(r => r.ToString()))}", important: false);
        }

        #endregion COMMAND_SAR_DELETE

        #region COMMAND_SAR_DELETEALL

        [Command("deleteall"), UsageInteractivity]
        [Description("Delete all self-assignable roles for the current guild.")]
        [Aliases("removeall", "rmrf", "rma", "clearall", "clear", "delall", "da")]
        public async Task DeleteAllAsync(CommandContext ctx)
        {
            if (!await ctx.WaitForBoolReplyAsync("Are you sure you want to delete all self-assignable roles for this guild?").ConfigureAwait(false))
                return;

            using (var db = this.Database.CreateContext())
            {
                db.SelfAssignableRoles.RemoveRange(db.SelfAssignableRoles.Where(r => r.GuildId == ctx.Guild.Id));
                await db.SaveChangesAsync();
            }

            var logchn = this.Shared.GetLogChannelForGuild(ctx.Client, ctx.Guild);
            if (!(logchn is null))
            {
                var emb = new DiscordEmbedBuilder
                {
                    Title = "All self-assignable roles have been deleted",
                    Color = this.ModuleColor
                };
                emb.AddField("User responsible", ctx.User.Mention, inline: true);
                emb.AddField("Invoked in", ctx.Channel.Mention, inline: true);
                await logchn.SendMessageAsync(embed: emb.Build());
            }

            await this.InformAsync(ctx, "Removed all self-assignable roles for this guild!", important: false);
        }

        #endregion COMMAND_SAR_DELETEALL

        #region COMMAND_SAR_LIST

        [Command("list")]
        [Description("List all current self-assignable roles.")]
        [Aliases("print", "show", "ls", "l", "p")]
        public async Task ListAsync(CommandContext ctx)
        {
            var roles = new List<DiscordRole>();
            using (var dc = this.Database.CreateContext())
            {
                IReadOnlyList<ulong> rids = dc.SelfAssignableRoles
                    .Where(r => r.GuildId == ctx.Guild.Id)
                    .Select(r => r.RoleId)
                    .ToList()
                    .AsReadOnly();
                if (!rids.Any())
                    throw new CommandFailedException("This guild doesn't have any automatic roles set.");

                foreach (ulong rid in rids)
                {
                    var role = ctx.Guild.GetRole(rid);
                    if (role is null)
                    {
                        dc.SelfAssignableRoles.Remove(new DatabaseSelfRole
                        {
                            GuildId = ctx.Guild.Id,
                            RoleId = rid
                        });
                    } else
                    {
                        roles.Add(role);
                    }
                }

                await dc.SaveChangesAsync();
            }

            await ctx.SendCollectionInPagesAsync(
                "Self-Assignable roles for this guild:",
                roles.OrderByDescending(r => r.Position),
                r => r.Mention,
                this.ModuleColor
            );
        }

        #endregion COMMAND_SAR_LIST
    }
}

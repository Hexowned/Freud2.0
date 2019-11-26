#region USING_DIRECTIVES

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
    [Group("automaticroles"), Module(ModuleType.Administration), NotBlocked]
    [Description("Automatic roles management. Automatic roles are automatically granted to a new member " +
                 "of the guild. Group call lists all automatic roles for the guild. Group call with an " +
                 "arbitrary amount of roles will add those roles to the automatic roles list for the " +
                 "guild, effective immediately.")]
    [Aliases("autoroles", "automaticr", "autorole", "aroles", "arole", "arl", "ar", "aar")]
    [UsageExampleArgs("@Guests")]
    [RequireUserPermissions(Permissions.Administrator)]
    [Cooldown(3, 5, CooldownBucketType.Guild)]
    public class AutomaticRolesModule : FreudModule
    {
        public AutomaticRolesModule(SharedData shared, DatabaseContextBuilder dcb)
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

        #region COMMAND_AR_ADD

        [Command("add")]
        [Description("Add automatic role(s).")]
        [Aliases("a", "+", "+=", "<<", "<")]
        [UsageExampleArgs("@Notifications", "@Notifications @Role1 @Role2")]
        public async Task AddAsync(CommandContext ctx,
                                  [Description("Roles to add.")] params DiscordRole[] roles)
        {
            if (roles is null || !roles.Any())
                throw new InvalidCommandUsageException("Missing roles to add.");

            using (var dc = this.Database.CreateContext())
            {
                dc.AutoAssignableRoles.SafeAddRange(roles.Select(r => new DatabaseAutoRole
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
                    Title = "Automatic roles change occured",
                    Color = this.ModuleColor
                };
                emb.AddField("User responsible", ctx.User.Mention, inline: true);
                emb.AddField("Invoked in", ctx.Channel.Mention, inline: true);
                emb.AddField("Roles added", string.Join("\n", roles.Select(r => r.ToString())));
                await logchn.SendMessageAsync(embed: emb.Build());
            }

            await this.InformAsync(ctx, $"Added automatic roles:\n\n{string.Join("\n", roles.Select(r => r.ToString()))}", important: false);
        }

        #endregion COMMAND_AR_ADD

        #region COMMAND_AR_DELETE

        [Command("delete")]
        [Description("Remove automatic role(s).")]
        [Aliases("remove", "rm", "del", "d", "-", "-=", ">", ">>")]
        [UsageExampleArgs("@Notifications", "@Notifications @Role1 @Role2")]
        public async Task DeleteAsync(CommandContext ctx,
                                     [Description("Roles to remove.")] params DiscordRole[] roles)
        {
            if (roles is null || !roles.Any())
                throw new InvalidCommandUsageException("You need to specify roles to remove.");

            using (var dc = this.Database.CreateContext())
            {
                dc.AutoAssignableRoles.RemoveRange(dc.AutoAssignableRoles.Where(ar => ar.GuildId == ctx.Guild.Id && roles.Any(r => r.Id == ar.RoleId)));
                await dc.SaveChangesAsync();
            }

            var logchn = this.Shared.GetLogChannelForGuild(ctx.Client, ctx.Guild);
            if (!(logchn is null))
            {
                var emb = new DiscordEmbedBuilder
                {
                    Title = "Automatic roles change occured",
                    Color = this.ModuleColor
                };
                emb.AddField("User responsible", ctx.User.Mention, inline: true);
                emb.AddField("Invoked in", ctx.Channel.Mention, inline: true);
                emb.AddField("Roles removed", string.Join("\n", roles.Select(r => r.ToString())));
                await logchn.SendMessageAsync(embed: emb.Build());
            }

            await this.InformAsync(ctx, $"Removed automatic roles:\n\n{string.Join("\n", roles.Select(r => r.ToString()))}", important: false);
        }

        #endregion COMMAND_AR_DELETE

        #region COMMAND_AR_DELETEALL

        [Command("deleteall"), UsageInteractivity]
        [Description("Delete all automatic roles for this guild.")]
        [Aliases("removeall", "rmrf", "rma", "clearall", "clear", "delall", "da")]
        public async Task DeleteAllAsync(CommandContext ctx)
        {
            if (!await ctx.WaitForBoolReplyAsync("Are you sure you want to delete all automatic roles for this guild?"))
                return;

            using (var dc = this.Database.CreateContext())
            {
                dc.AutoAssignableRoles.RemoveRange(dc.AutoAssignableRoles.Where(r => r.GuildId == ctx.Guild.Id));
                await dc.SaveChangesAsync();
            }

            var logchn = this.Shared.GetLogChannelForGuild(ctx.Client, ctx.Guild);
            if (!(logchn is null))
            {
                var emb = new DiscordEmbedBuilder
                {
                    Title = "All automatic roles have been deleted",
                    Color = this.ModuleColor
                };
                emb.AddField("User responsible", ctx.User.Mention, inline: true);
                emb.AddField("Invoked in", ctx.Channel.Mention, inline: true);
                await logchn.SendMessageAsync(embed: emb.Build());
            }

            await this.InformAsync(ctx, "Removed all automatic roles for this guild!", important: false);
        }

        #endregion COMMAND_AR_DELETEALL

        #region COMMAND_AR_LIST

        [Command("list")]
        [Description("List all current automatic roles.")]
        [Aliases("print", "show", "ls", "l", "p")]
        public async Task ListAsync(CommandContext ctx)
        {
            var roles = new List<DiscordRole>();

            using (var dc = this.Database.CreateContext())
            {
                IReadOnlyList<ulong> rids = dc.AutoAssignableRoles
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
                        dc.AutoAssignableRoles.Remove(new DatabaseAutoRole
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
                "Automatic roles for this guild:",
                roles.OrderByDescending(r => r.Position),
                r => r.Mention,
                this.ModuleColor
            );
        }

        #endregion COMMAND_AR_LIST
    }
}

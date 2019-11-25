﻿#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Freud.Common.Attributes;
using Freud.Database.Db;
using Freud.Exceptions;
using Freud.Extensions;
using Freud.Extensions.Discord;
using System;
using System.Linq;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Administration
{
    [Group("roles"), Module(ModuleType.Administration), NotBlocked]
    [Description("Miscellaneous role control commands. Group call lists all the roles in this guild or prints information about a given role.")]
    [Aliases("role", "rl")]
    [UsageExampleArgs("SomeRole")]
    [Cooldown(3, 5, CooldownBucketType.Guild)]
    public class RoleModule : FreudModule
    {
        public RoleModule(SharedData shared, DatabaseContextBuilder dcb)
            : base(shared, dcb)
        {
            this.ModuleColor = DiscordColor.Lilac;
        }

        [GroupCommand, Priority(1)]
        public Task ExecuteGroupAsync(CommandContext ctx)
        {
            return ctx.SendCollectionInPagesAsync(
                "Roles in this guild:",
                ctx.Guild.Roles.Values.OrderByDescending(r => r.Position),
                r => $"{Formatter.InlineCode(r.Id.ToString())} | {(r.Mention)}",
                this.ModuleColor,
                10
            );
        }

        [GroupCommand, Priority(0)]
        public Task ExecuteGroupAsync(CommandContext ctx,
                                     [Description("Role.")] DiscordRole role)
            => this.InfoAsync(ctx, role);

        #region COMMAND_ROLES_CREATE

        [Command("create"), Priority(2), UsageInteractivity]
        [Description("Create a new role.")]
        [Aliases("new", "add", "a", "c", "+", "+=", "<", "<<")]
        [UsageExampleArgs("Role", "\"My role\" #C77B0F no no", "#C77B0F My new role")]
        [RequirePermissions(Permissions.ManageRoles)]
        public async Task CreateAsync(CommandContext ctx,
                                     [Description("Name.")] string name,
                                     [Description("Color.")] DiscordColor? color = null,
                                     [Description("Hoisted (visible in online list)?")] bool hoisted = false,
                                     [Description("Mentionable?")] bool mentionable = false)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidCommandUsageException("Missing role name.");

            if (ctx.Guild.Roles.Values.Any(r => string.Compare(r.Name, name, true) == 0))
            {
                if (!await ctx.WaitForBoolReplyAsync("A role with that name already exists. Continue?"))
                    return;
            }

            var role = await ctx.Guild.CreateRoleAsync(name, null, color, hoisted, mentionable, ctx.BuildInvocationDetailsString());
            await this.InformAsync(ctx, $"Successfully created role: {Formatter.Bold(role.Name)}", important: false);
        }

        [Command("create"), Priority(1)]
        public Task CreateAsync(CommandContext ctx,
                               [Description("Color.")] DiscordColor color,
                               [RemainingText, Description("Name.")] string name)
            => this.CreateAsync(ctx, name, color, false, false);

        [Command("create"), Priority(0)]
        public Task CreateAsync(CommandContext ctx,
                               [RemainingText, Description("Name.")] string name)
            => this.CreateAsync(ctx, name, null, false, false);

        #endregion COMMAND_ROLES_CREATE

        #region COMMAND_ROLES_DELETE

        [Command("delete")]
        [Description("Create a new role.")]
        [Aliases("del", "remove", "rm", "d", "-", ">", ">>")]
        [UsageExampleArgs("My role", "@Admins")]
        [RequirePermissions(Permissions.ManageRoles)]
        public async Task DeleteAsync(CommandContext ctx,
                                     [Description("Role.")] DiscordRole role,
                                     [RemainingText, Description("Reason.")] string reason = null)
        {
            string name = Formatter.Bold(role.Name);
            await role.DeleteAsync(ctx.BuildInvocationDetailsString(reason));
            await this.InformAsync(ctx, $"Successfully deleted role: {Formatter.Bold(name)}", important: false);
        }

        #endregion COMMAND_ROLES_DELETE

        #region COMMAND_ROLES_INFO

        [Command("info")]
        [Description("Get information about a given role.")]
        [Aliases("i")]
        [UsageExampleArgs("Admins")]
        [RequirePermissions(Permissions.ManageRoles)]
        public Task InfoAsync(CommandContext ctx,
                             [Description("Role.")] DiscordRole role)
        {
            var emb = new DiscordEmbedBuilder
            {
                Title = role.Name,
                Color = this.ModuleColor
            };

            emb.AddField("Position", role.Position.ToString(), true);
            emb.AddField("Color", role.Color.ToString(), true);
            emb.AddField("Id", role.Id.ToString(), true);
            emb.AddField("Mentionable", role.IsMentionable.ToString(), true);
            emb.AddField("Visible", role.IsHoisted.ToString(), true);
            emb.AddField("Managed", role.IsManaged.ToString(), true);
            emb.AddField("Created at", role.CreationTimestamp.ToUtcTimestamp(), true);
            emb.AddField("Permissions:", role.Permissions.ToPermissionString());

            return ctx.RespondAsync(embed: emb.Build());
        }

        #endregion COMMAND_ROLES_INFO

        #region COMMAND_ROLES_MENTION

        [Command("mention")]
        [Description("Mention the given role. This will bypass the mentionable status for the given role.")]
        [Aliases("mentionall", "@", "ma")]
        [UsageExampleArgs("Admins")]
        [RequireUserPermissions(Permissions.Administrator), RequireBotPermissions(Permissions.ManageRoles)]
        public async Task MentionAllFromRoleAsync(CommandContext ctx,
                                                 [Description("Role.")] DiscordRole role)
        {
            if (role.IsMentionable)
            {
                await ctx.RespondAsync(role.Mention);
                return;
            }

            await role.ModifyAsync(mentionable: true);
            await ctx.RespondAsync(role.Mention);
            await role.ModifyAsync(mentionable: false);
        }

        #endregion COMMAND_ROLES_MENTION

        #region COMMAND_ROLES_SETCOLOR

        [Command("setcolor"), Priority(1)]
        [Description("Set a color for the role.")]
        [Aliases("clr", "c", "sc", "setc")]
        [UsageExampleArgs("#FF0000 Admins", "Admins #FF0000")]
        [RequirePermissions(Permissions.ManageRoles)]
        public async Task SetColorAsync(CommandContext ctx,
                                       [Description("Role.")] DiscordRole role,
                                       [Description("Color.")] DiscordColor color)
        {
            await role.ModifyAsync(color: color, reason: ctx.BuildInvocationDetailsString());
            await this.InformAsync(ctx, $"Successfully set the color for the role {Formatter.Bold(role.Name)} to {Formatter.InlineCode(role.Color.ToString())}", important: false);
        }

        [Command("setcolor"), Priority(0)]
        public Task SetColorAsync(CommandContext ctx,
                                 [Description("Color.")] DiscordColor color,
                                 [Description("Role.")] DiscordRole role)
            => this.SetColorAsync(ctx, role, color);

        #endregion COMMAND_ROLES_SETCOLOR

        #region COMMAND_ROLES_SETNAME

        [Command("setname"), Priority(1)]
        [Description("Set a name for the role.")]
        [Aliases("name", "rename", "n")]
        [UsageExampleArgs("@Admins Administrators", "Administrators @Admins")]
        [RequirePermissions(Permissions.ManageRoles)]
        public async Task RenameAsync(CommandContext ctx,
                                     [Description("Role.")] DiscordRole role,
                                     [RemainingText, Description("New name.")] string newname)
        {
            if (string.IsNullOrWhiteSpace(newname))
                throw new ArgumentException("I need a new name for the role.");

            string name = role.Name;
            await role.ModifyAsync(name: newname, reason: ctx.BuildInvocationDetailsString());
            await this.InformAsync(ctx, $"Successfully renamed role {Formatter.Bold(name)} to {Formatter.Bold(role.Name)}", important: false);
        }

        [Command("setname"), Priority(0)]
        public Task RenameAsync(CommandContext ctx,
                               [Description("New name.")] string name,
                               [Description("Role.")] DiscordRole role)
            => this.RenameAsync(ctx, role, name);

        #endregion COMMAND_ROLES_SETNAME

        #region COMMAND_ROLES_SETMENTIONABLE

        [Command("setmentionable"), Priority(1)]
        [Description("Set role mentionable var.")]
        [Aliases("mentionable", "m", "setm")]
        [UsageExampleArgs("Admins", "Admins on", "off Admins")]
        [RequirePermissions(Permissions.ManageRoles)]
        public async Task SetMentionableAsync(CommandContext ctx,
                                             [Description("Role.")] DiscordRole role,
                                             [Description("Mentionable?")] bool mentionable = true)
        {
            await role.ModifyAsync(mentionable: mentionable, reason: ctx.BuildInvocationDetailsString());
            await this.InformAsync(ctx, $"Mentionable var for role {Formatter.Bold(role.Name)} is set to {Formatter.InlineCode(mentionable.ToString())}", important: false);
        }

        [Command("setmentionable"), Priority(0)]
        public Task SetMentionableAsync(CommandContext ctx,
                                       [Description("Mentionable?")] bool mentionable,
                                       [Description("Role.")] DiscordRole role)
            => this.SetMentionableAsync(ctx, role, mentionable);

        #endregion COMMAND_ROLES_SETMENTIONABLE

        #region COMMAND_ROLES_SETVISIBILITY

        [Command("setvisible"), Priority(1)]
        [Description("Set role hoisted var (visibility in online list).")]
        [Aliases("separate", "h", "seth", "hoist", "sethoist")]
        [UsageExampleArgs("Admins", "Admins off", "on Admins")]
        [RequirePermissions(Permissions.ManageRoles)]
        public async Task SetVisibleAsync(CommandContext ctx,
                                         [Description("Role.")] DiscordRole role,
                                         [Description("Hoisted (visible in online list)?")] bool hoisted = false)
        {
            await role.ModifyAsync(hoist: hoisted, reason: ctx.BuildInvocationDetailsString());
            await this.InformAsync(ctx, $"Visibility (hoist) var for role {Formatter.Bold(role.Name)} is set to {Formatter.InlineCode(hoisted.ToString())}", important: false);
        }

        [Command("setvisible"), Priority(0)]
        public Task SetVisibleAsync(CommandContext ctx,
                                   [Description("Hoisted (visible in online list)?")] bool hoisted,
                                   [Description("Role.")] DiscordRole role)
            => this.SetVisibleAsync(ctx, role, hoisted);

        #endregion COMMAND_ROLES_SETVISIBILITY
    }
}

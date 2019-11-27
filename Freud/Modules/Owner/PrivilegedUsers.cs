#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using Freud.Common.Attributes;
using Freud.Database.Db;
using Freud.Database.Db.Entities;
using Freud.Exceptions;
using Freud.Extensions.Discord;
using Freud.Modules.Administration.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Owner
{
    public partial class OwnerModule
    {
        [Group("privilegedusers"), NotBlocked]
        [Description("Manipulate privileged users. Privileged users can invoke commands marked with RequirePrivilegedUsers permission.")]
        [Aliases("pu", "privu", "privuser", "pusers", "puser", "pusr")]
        [RequireOwner]
        public class PrivilegedUsersModule : FreudModule
        {
            public PrivilegedUsersModule(SharedData shared, DatabaseContextBuilder dcb)
                : base(shared, dcb)
            {
                this.ModuleColor = DiscordColor.NotQuiteBlack;
            }

            [GroupCommand, Priority(1)]
            public Task ExecuteGroupAsync(CommandContext ctx)
                => this.ListAsync(ctx);

            [GroupCommand, Priority(0)]
            public Task ExecuteGroupAsync(CommandContext ctx,
                                         [Description("Users to grant privilege to.")] params DiscordUser[] users)
                => this.AddAsync(ctx, users);

            #region COMMAND_PRIVILEGED_USERS_ADD

            [Command("add")]
            [Description("Add users to privileged users list.")]
            [Aliases("+", "a", "<", "<<", "+=")]
            [UsageExampleArgs("add @Someone", "add @Someone @SomeoneElse")]
            public async Task AddAsync(CommandContext ctx,
                                      [Description("Users to grant privilege to.")] params DiscordUser[] users)
            {
                if (users is null || !users.Any())
                    throw new InvalidCommandUsageException("Missing users to grant privilege to.");

                using (var dc = this.Database.CreateContext())
                {
                    dc.PrivilegedUsers.SafeAddRange(users.Select(u => new DatabasePrivilegedUser { UserId = u.Id }));
                    await dc.SaveChangesAsync();
                }

                await this.InformAsync(ctx, "Granted privilege to all of the given users.", important: false);
            }

            #endregion COMMAND_PRIVILEGED_USERS_ADD

            #region COMMAND_PRIVILEGED_USERS_DELETE

            [Command("delete")]
            [Description("Remove users from privileged users list.")]
            [Aliases("-", "remove", "rm", "del", ">", ">>", "-=")]
            [UsageExampleArgs("remove @Someone", "remove 123123123123123", "remove @Someone 123123123123123")]
            public async Task DeleteAsync(CommandContext ctx,
                                         [Description("Users to revoke privileges from.")] params DiscordUser[] users)
            {
                if (users is null || !users.Any())
                    throw new InvalidCommandUsageException("Missing users");

                using (var dc = this.Database.CreateContext())
                {
                    dc.PrivilegedUsers.RemoveRange(dc.PrivilegedUsers.Where(pu => users.Any(u => u.Id == pu.UserId)));
                    await dc.SaveChangesAsync();
                }

                await this.InformAsync(ctx, "Revoked privilege from all given users.", important: false);
            }

            #endregion COMMAND_PRIVILEGED_USERS_DELETE

            #region COMMAND_PRIVILEGED_USERS_LIST

            [Command("list")]
            [Description("List all privileged users.")]
            [Aliases("ls", "l", "print")]
            public async Task ListAsync(CommandContext ctx)
            {
                List<DatabasePrivilegedUser> privileged;
                using (var dc = this.Database.CreateContext())
                    privileged = await dc.PrivilegedUsers.ToListAsync();

                var valid = new List<DiscordUser>();
                foreach (var usr in privileged)
                {
                    try
                    {
                        var user = await ctx.Client.GetUserAsync(usr.UserId);
                        valid.Add(user);
                    } catch (NotFoundException)
                    {
                        this.Shared.LogProvider.Log(LogLevel.Debug, $"Removed 404 privileged user with ID {usr.UserId}");
                        using (var dc = this.Database.CreateContext())
                        {
                            dc.PrivilegedUsers.Remove(new DatabasePrivilegedUser { UserIdDb = usr.UserIdDb });
                            await dc.SaveChangesAsync();
                        }
                    }
                }

                if (!valid.Any())
                    throw new CommandFailedException("No privileged users were registered!");

                await ctx.SendCollectionInPagesAsync("Privileged users", valid, user => user.ToString(), this.ModuleColor, 10);
            }

            #endregion COMMAND_PRIVILEGED_USERS_LIST
        }
    }
}

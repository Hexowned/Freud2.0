#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Freud.Common.Attributes;
using Freud.Common.Configuration;
using Freud.Database.Db;
using Freud.Database.Db.Entities;
using Freud.Exceptions;
using Freud.Modules.Administration.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Administration.Configuration
{
    public partial class GuildModule
    {
        public partial class GuildConfigurationModule
        {
            [Group("logging")]
            [Description("Action logging configuration.")]
            [Aliases("log", "modlog")]
            [UsageExampleArgs("on #log", "off")]
            public class LoggingModule : FreudModule
            {
                public LoggingModule(SharedData shared, DatabaseContextBuilder dcb)
                    : base(shared, dcb)
                {
                    this.ModuleColor = DiscordColor.DarkRed;
                }

                [GroupCommand, Priority(1)]
                public async Task ExecuteGroupAsync(CommandContext ctx,
                                                   [Description("Enable?")] bool enable,
                                                   [Description("Log channel.")] DiscordChannel channel = null)
                {
                    channel = channel ?? ctx.Channel;

                    if (channel.Type != ChannelType.Text)
                        throw new CommandFailedException("Action logging channel must be a text channel.");

                    DatabaseGuildConfiguration gcfg = await this.ModifyGuildConfigurationAsync(ctx.Guild.Id, cfg =>
                    {
                        cfg.LogChannelIdDb = enable ? (long?)channel.Id : null;
                    });

                    var logchn = this.Shared.GetLogChannelForGuild(ctx.Client, ctx.Guild);
                    if (!(logchn is null))
                    {
                        var emb = new DiscordEmbedBuilder
                        {
                            Title = "Guild config changed",
                            Color = this.ModuleColor
                        };
                        emb.AddField("User responsible", ctx.User.Mention, inline: true);
                        emb.AddField("Invoked in", ctx.Channel.Mention, inline: true);
                        emb.AddField("Logging channel set to", gcfg.LogChannelId.ToString(), inline: true);
                        await logchn.SendMessageAsync(embed: emb.Build());
                    }

                    await this.InformAsync(ctx, $"{Formatter.Bold(gcfg.LoggingEnabled ? "Enabled" : "Disabled")} action logs.", important: false);
                }

                [GroupCommand, Priority(0)]
                public async Task ExecuteGroupAsync(CommandContext ctx)
                {
                    var gcfg = this.Shared.GetGuildConfiguration(ctx.Guild.Id);
                    if (gcfg.LoggingEnabled)
                    {
                        var sb = new StringBuilder();
                        sb.Append(Formatter.Bold("Exempts:"));

                        List<DatabaseExemptLogging> exempted;
                        using (var dc = this.Database.CreateContext())
                        {
                            exempted = await dc.LoggingExempts
                                .Where(ee => ee.GuildId == ctx.Guild.Id)
                                .OrderBy(ee => ee.Type)
                                .ToListAsync();
                        }

                        if (exempted.Any())
                        {
                            sb.AppendLine();
                            foreach (DatabaseExemptedEntity ee in exempted)
                                sb.AppendLine($"{ee.Type.ToUserFriendlyString()}: {ee.Id}");
                        } else
                        {
                            sb.Append(" None");
                        }
                        await this.InformAsync(ctx, $"Action logging for this guild is {Formatter.Bold("enabled")} at {ctx.Guild.GetChannel(gcfg.LogChannelId)?.Mention ?? "(unknown)"}!\n\n{sb.ToString()}");
                    } else
                    {
                        await this.InformAsync(ctx, $"Action logging for this guild is {Formatter.Bold("disabled")}!");
                    }
                }

                #region COMMAND_LOGGING_EXEMPT

                [Command("exempt"), Priority(2)]
                [Description("Disable the logs for some entities (users, channels, etc).")]
                [Aliases("ex", "exc")]
                [UsageExampleArgs("@Someone", "#spam", "Role")]
                public async Task ExemptAsync(CommandContext ctx,
                                             [Description("Members to exempt.")] params DiscordMember[] members)
                {
                    if (members is null || !members.Any())
                        throw new CommandFailedException("You need to provide users or channels or roles to exempt.");

                    using (var dc = this.Database.CreateContext())
                    {
                        dc.LoggingExempts.AddExemptions(ctx.Guild.Id, members, ExemptedEntityType.Member);
                        await dc.SaveChangesAsync();
                    }

                    await this.InformAsync(ctx, "Successfully exempted given users.", important: false);
                }

                [Command("exempt"), Priority(1)]
                public async Task ExemptAsync(CommandContext ctx,
                                             [Description("Roles to exempt.")] params DiscordRole[] roles)
                {
                    if (roles is null || !roles.Any())
                        throw new CommandFailedException("You need to provide users or channels or roles to exempt.");

                    using (var dc = this.Database.CreateContext())
                    {
                        dc.LoggingExempts.AddExemptions(ctx.Guild.Id, roles, ExemptedEntityType.Role);
                        await dc.SaveChangesAsync();
                    }

                    await this.InformAsync(ctx, "Successfully exempted given roles.", important: false);
                }

                [Command("exempt"), Priority(0)]
                public async Task ExemptAsync(CommandContext ctx,
                                             [Description("Channels to exempt.")] params DiscordChannel[] channels)
                {
                    if (channels is null || !channels.Any())
                        throw new CommandFailedException("You need to provide users or channels or roles to exempt.");

                    using (var dc = this.Database.CreateContext())
                    {
                        dc.LoggingExempts.AddExemptions(ctx.Guild.Id, channels, ExemptedEntityType.Channel);
                        await dc.SaveChangesAsync();
                    }

                    await this.InformAsync(ctx, "Successfully exempted given channels.", important: false);
                }

                #endregion COMMAND_LOGGING_EXEMPT

                #region COMMAND_LOGGING_UNEXEMPT

                [Command("unexempt"), Priority(2)]
                [Description("Remove an exempted entity and allow logging for actions regarding that entity.")]
                [Aliases("unex", "uex")]
                [UsageExampleArgs("@Someone", "#spam", "Role")]
                public async Task UnxemptAsync(CommandContext ctx,
                                             [Description("Members to unexempt.")] params DiscordMember[] members)
                {
                    if (members is null || !members.Any())
                        throw new CommandFailedException("You need to provide users or channels or roles to exempt.");

                    using (var dc = this.Database.CreateContext())
                    {
                        dc.LoggingExempts.RemoveRange(
                            dc.LoggingExempts.Where(ex => ex.GuildId == ctx.Guild.Id && ex.Type == ExemptedEntityType.Member && members.Any(m => m.Id == ex.Id))
                        );
                        await dc.SaveChangesAsync();
                    }

                    await this.InformAsync(ctx, "Successfully unexempted given users.", important: false);
                }

                [Command("unexempt"), Priority(1)]
                public async Task UnxemptAsync(CommandContext ctx,
                                              [Description("Roles to unexempt.")] params DiscordRole[] roles)
                {
                    if (roles is null || !roles.Any())
                        throw new CommandFailedException("You need to provide users or channels or roles to exempt.");

                    using (var dc = this.Database.CreateContext())
                    {
                        dc.LoggingExempts.RemoveRange(
                            dc.LoggingExempts.Where(ex => ex.GuildId == ctx.Guild.Id && ex.Type == ExemptedEntityType.Role && roles.Any(r => r.Id == ex.Id))
                        );
                        await dc.SaveChangesAsync();
                    }

                    await this.InformAsync(ctx, "Successfully unexempted given roles.", important: false);
                }

                [Command("unexempt"), Priority(0)]
                public async Task UnxemptAsync(CommandContext ctx,
                                              [Description("Channels to unexempt.")] params DiscordChannel[] channels)
                {
                    if (channels is null || !channels.Any())
                        throw new CommandFailedException("You need to provide users or channels or roles to exempt.");

                    using (var dc = this.Database.CreateContext())
                    {
                        dc.LoggingExempts.RemoveRange(
                            dc.LoggingExempts.Where(ex => ex.GuildId == ctx.Guild.Id && ex.Type == ExemptedEntityType.Channel && channels.Any(c => c.Id == ex.Id))
                        );
                        await dc.SaveChangesAsync();
                    }

                    await this.InformAsync(ctx, "Successfully unexempted given channels.", important: false);
                }

                #endregion COMMAND_LOGGING_UNEXEMPT
            }
        }
    }
}

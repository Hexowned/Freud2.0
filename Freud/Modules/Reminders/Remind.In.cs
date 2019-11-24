#region USING_DIRECTIVES

using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Freud.Common.Attributes;
using Freud.Database.Db;
using System;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Reminders
{
    public partial class RemindModule
    {
        [Group("in")]
        [Description("Send a reminder after a specific time span")]
        [UsageExamplesAttributes("3h Do 50 pushups!", "3h30m Do 50 pushups!")]
        public class RemindInModule : RemindModule
        {
            public RemindInModule(SharedData shared, DatabaseContextBuilder dcb)
                : base(shared, dcb)
            {
                this.ModuleColor = DiscordColor.NotQuiteBlack;
            }

            [GroupCommand, Priority(2)]
            public new Task ExecuteGroupAsync(CommandContext ctx,
                                              [Description("Time span until reminder.")] TimeSpan timespan,
                                              [Description("Channel to send message to.")] DiscordChannel channel,
                                              [RemainingText, Description("What to send?")] string message)
                => this.AddReminderAsync(ctx, timespan, channel, message);

            [GroupCommand, Priority(1)]
            public new Task ExecuteGroupAsync(CommandContext ctx,
                                             [Description("Channel to send message to.")] DiscordChannel channel,
                                             [Description("Time span until reminder.")] TimeSpan timespan,
                                             [RemainingText, Description("What to send?")] string message)
                => this.AddReminderAsync(ctx, timespan, channel, message);

            [GroupCommand, Priority(0)]
            public new Task ExecuteGroupAsync(CommandContext ctx,
                                             [Description("Time span until reminder.")] TimeSpan timespan,
                                             [RemainingText, Description("What to send?")] string message)
                => this.AddReminderAsync(ctx, timespan, null, message);
        }
    }
}

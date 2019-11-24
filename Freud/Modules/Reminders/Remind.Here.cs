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
        [Group("here")]
        [Description("Send a reminder to the current channel after a specific time span.")]
        [UsageExamplesAttributes("3h Do 50 pushups!", "3h30m Do pushups!")]
        public class RemindHereModule : RemindModule
        {
            public RemindHereModule(SharedData shared, DatabaseContextBuilder dcb)
                : base(shared, dcb)
            {
                this.ModuleColor = DiscordColor.NotQuiteBlack;
            }

            [GroupCommand, Priority(1)]
            public new Task ExecuteGroupAsync(CommandContext ctx,
                                             [Description("Time span until the reminder.")] TimeSpan timespan,
                                             [RemainingText, Description("What to send?")] string message)
                => this.AddReminderAsync(ctx, timespan, ctx.Channel, message);

            [GroupCommand, Priority(0)]
            public Task ExecuteGroupAsync(CommandContext ctx)
                => this.ListAsync(ctx, ctx.Channel);

            [Group("in")]
            [Description("Send a reminder to the current channel after a specific time span.")]
            [UsageExamplesAttributes("3h Do 50 pushups!", "3h30m Do pushups!")]
            public class RemindHereInModule : RemindHereModule
            {
                public RemindHereInModule(SharedData shared, DatabaseContextBuilder dcb)
                    : base(shared, dcb)
                {
                    this.ModuleColor = DiscordColor.NotQuiteBlack;
                }

                [GroupCommand]
                public new Task ExecuteGroupAsync(CommandContext ctx,
                                                 [Description("Time span until reminder.")] TimeSpan timespan,
                                                 [RemainingText, Description("What to send?")] string message)
                    => this.AddReminderAsync(ctx, timespan, ctx.Channel, message);
            }

            [Group("at")]
            [Description("Send a reminder to the current channel at a specific point in time (given by date and time string).")]
            [UsageExamplesAttributes("\"12.07.2019 00:00\" Do")]
            public class RemindHereAtModule : RemindModule
            {
                public RemindHereAtModule(SharedData shared, DatabaseContextBuilder dcb)
                    : base(shared, dcb)
                {
                    this.ModuleColor = DiscordColor.NotQuiteBlack;
                }

                [GroupCommand, Priority(0)]
                public Task ExecuteGroupAsync(CommandContext ctx,
                                             [Description("Date and/or time.")] DateTimeOffset when,
                                             [RemainingText, Description("What to send?")] string message)
                    => this.AddReminderAsync(ctx, when - DateTimeOffset.Now, ctx.Channel, message);
            }
        }
    }
}

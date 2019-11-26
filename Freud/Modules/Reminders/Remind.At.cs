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
        [Group("at")]
        [Description("Send a reminder at a specific point in time (given by date and time string).")]
        [UsageExampleArgs("17:30 Start homework!", "12.18.2019 Meeting at 9", "\"12.10.2019 09:00\" Study for test!")]
        public class RemindAtModule : RemindModule
        {
            public RemindAtModule(SharedData shared, DatabaseContextBuilder dcb)
                : base(shared, dcb)
            {
                this.ModuleColor = DiscordColor.NotQuiteBlack;
            }

            [GroupCommand, Priority(2)]
            public Task ExecuteGroupAsync(CommandContext ctx,
                                             [Description("Date and/or time.")] DateTimeOffset when,
                                             [Description("Channel to send message to.")] DiscordChannel channel,
                                             [RemainingText, Description("What to send?")] string message)
                    => this.AddReminderAsync(ctx, when - DateTimeOffset.Now, channel, message);

            [GroupCommand, Priority(1)]
            public Task ExecuteGroupAsync(CommandContext ctx,
                                             [Description("Channel to send message to.")] DiscordChannel channel,
                                             [Description("Date and/or time.")] DateTimeOffset when,
                                             [RemainingText, Description("What to send?")] string message)
                    => this.AddReminderAsync(ctx, when - DateTimeOffset.Now, channel, message);

            [GroupCommand, Priority(0)]
            public Task ExecuteGroupAsync(CommandContext ctx,
                                             [Description("Date and/or time.")] DateTimeOffset when,
                                             [RemainingText, Description("What to send?")] string message)
                    => this.AddReminderAsync(ctx, when - DateTimeOffset.Now, null, message);
        }
    }
}

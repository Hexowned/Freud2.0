#region USING_DIRECTIVES

using Freud.Common.Tasks;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#endregion USING_DIRECTIVES

namespace Freud.Database.Db.Entities
{
    [Table("reminders")]
    public class DatabaseReminder
    {
        public static DatabaseReminder FromSavedTaskInfo(SavedTaskInfo tinfo)
        {
            var minfo = tinfo as SendMessageTaskInfo;
            if (minfo is null)
                return null;

            var reminder = new DatabaseReminder
            {
                ChannelId = minfo.ChannelId,
                ExecutionTime = tinfo.ExecutionTime.UtcDateTime,
                IsRepeating = minfo.IsRepeating,
                Message = minfo.Message,
                RepeatIntervalDb = minfo.RepeatingInterval,
                UserId = minfo.InitiatorId
            };

            return reminder;
        }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("uid")]
        public long UserIdDb { get; set; }

        [NotMapped]
        public ulong UserId { get => (ulong)this.UserIdDb; set => this.UserIdDb = (long)value; }

        [Column("cid")]
        public long? ChannelIdDb { get; set; }

        [NotMapped]
        public ulong ChannelId { get => (ulong)this.ChannelIdDb.GetValueOrDefault(); set => this.ChannelIdDb = (long)value; }

        [Column("message"), Required, MaxLength(256)]
        public string Message { get; set; }

        [Column("execution_time", TypeName = "timestamptz")]
        public DateTimeOffset ExecutionTime { get; set; }

        [Column("is_repeating")]
        public bool IsRepeating { get; set; } = false;

        [Column("repeat_interval", TypeName = "interval")]
        public TimeSpan? RepeatIntervalDb { get; set; }

        [NotMapped]
        public TimeSpan RepeatInterval => this.RepeatIntervalDb ?? TimeSpan.FromMilliseconds(-1);
    }
}

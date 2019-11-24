#region USING_DIRECTIVES

using System;

#endregion USING_DIRECTIVES

namespace Freud.Extensions
{
    internal static class DateTimeExtensions
    {
        public static string ToUtcTimestamp(this DateTime datetime)
            => $"At {datetime.ToUniversalTime().ToString()} UTC";

        public static string ToUtcTimestamp(this DateTimeOffset datetime)
            => $"At {datetime.ToUniversalTime().ToString()} UTC";
    }
}

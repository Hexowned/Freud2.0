namespace Freud.Modules.Administration
{
    public enum ExemptedEntityType : byte
    {
        Channel = 0,
        Member = 1,
        Role = 2
    }

    public static class EntityTypeExtensions
    {
        public static char ToFlag(this ExemptedEntity entity)
        {
            switch (entity)
            {
                case ExemptedEntity.Channel: return 'c';
                case ExemptedEntity.Member: return 'm';
                case ExemptedEntity.Role: return 'r';
                default: return '?';
            }
        }

        public static string ToUserFriendlyString(this ExemptedEntity entity)
        {
            switch (entity)
            {
                case ExemptedEntity.Channel: return "Channel";
                case ExemptedEntity.Member: return "User";
                case ExemptedEntity.Role: return "Role";
                default: return "Unknown";
            }
        }
    }

    public sealed class ExemptedEntity
    {
        public ulong GuildId { get; set; }
        public ulong Id { get; set; }
        public ExemptedEntity Type { get; set; }
    }
}

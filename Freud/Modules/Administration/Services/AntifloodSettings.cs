using Freud.Modules.Administration.Common;

namespace Freud.Modules.Administration.Services
{
    public sealed class AntifloodSettings
    {
        public PunishmentActionType Action { get; set; } = PunishmentActionType.PermanentBan;
        public short Cooldown { get; set; } = 10;
        public bool Enabled { get; set; } = false;
        public short Sensitivity { get; set; } = 5;
    }
}

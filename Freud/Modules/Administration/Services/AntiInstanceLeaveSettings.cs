namespace Freud.Modules.Administration.Services
{
    public sealed class AntiInstantLeaveSettings
    {
        public bool Enabled { get; set; } = false;
        public short Cooldown { get; set; } = 3;
    }
}

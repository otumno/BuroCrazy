namespace Managers.Teletype
{
    public enum TeletypeMessageType
    {
        Info,
        Warning,
        Success,
        Important,
        Policy,
        StaffWork,
        Music
    }

    public static class TeletypeMessageTypeExtensions
    {
        public static string GetColor(this TeletypeMessageType type)
        {
            switch (type)
            {
                case TeletypeMessageType.Warning: return "#FFAA00";
                case TeletypeMessageType.Success: return "#44FF44";
                case TeletypeMessageType.Important: return "#FF4444";
                case TeletypeMessageType.Policy: return "#AA44FF";
                case TeletypeMessageType.StaffWork: return "#44AAFF";
                case TeletypeMessageType.Music: return "#FFD700";
                default: return "#FFFFFF";
            }
        }
    }
}
namespace Managers.Teletype
{
    public enum TeletypeMessageType
    {
        Info,
        Warning,
        Success,
        Important,
        Policy
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
                default: return "#FFFFFF";
            }
        }
    }
}
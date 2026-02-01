using System;

namespace Managers.Teletype
{
    public class TeletypeMessage
    {
        public readonly string Text;
        public readonly TeletypeMessageType Type;
        public readonly DateTime Timestamp;
        public readonly bool Persistant;

        public TeletypeMessage(string text, TeletypeMessageType type, DateTime timestamp, bool persistant)
        {
            Text = text;
            Timestamp = timestamp;
            Persistant = persistant;
            Type = type;
        }
    }
}
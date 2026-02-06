using UnityEngine;
using TMPro;
using Managers.Teletype;
using Data.Calendar;
using Managers;

namespace UI.Teletype
{
    public abstract class TeletypeMessageFormatter : MonoBehaviour
    {
        public abstract bool CanHandle(TeletypeMessageType type);
        public abstract string Format(TeletypeMessage msg);
        public virtual Color? GetTypeColor(TeletypeMessageType type) => null;
    }

    public class DefaultTeletypeFormatter : TeletypeMessageFormatter
    {
        public override bool CanHandle(TeletypeMessageType type) => true;

        public override string Format(TeletypeMessage msg)
        {
            var period = TimeManager.Instance.GetCurrentPeriodType();
            string periodPrefix = period.GetLocalization();
            string typeColor = msg.Type.GetColor();
            string timestamp = msg.Timestamp.ToString("HH:mm");
            return $"<color={typeColor}>[{timestamp}] [{periodPrefix}]</color> {msg.Text}";
        }
    }

    public class CompactTeletypeFormatter : TeletypeMessageFormatter
    {
        public override bool CanHandle(TeletypeMessageType type) => true;

        public override string Format(TeletypeMessage msg)
        {
            string typeColor = msg.Type.GetColor();
            return $"<color={typeColor}>{msg.Text}</color>";
        }
    }

    public class DetailedTeletypeFormatter : TeletypeMessageFormatter
    {
        public override bool CanHandle(TeletypeMessageType type) => true;

        public override string Format(TeletypeMessage msg)
        {
            string typeColor = msg.Type.GetColor();
            var period = TimeManager.Instance.GetCurrentPeriodType();
            string periodPrefix = period.GetLocalization();
            string timestamp = msg.Timestamp.ToString("HH:mm:ss");
            return $"<color={typeColor}>[{timestamp}] [{periodPrefix}] [{msg.Type}]</color>\n{msg.Text}";
        }
    }
}

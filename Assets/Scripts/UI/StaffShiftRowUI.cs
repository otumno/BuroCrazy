using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class StaffShiftRowUI : MonoBehaviour
    {
        [SerializeField] private StaffPortraitUI portrait;
        [SerializeField] private Image roleColor;
        [SerializeField] private Image portraitFrame;
        [SerializeField] private RoleColorDatabase roleColors;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text roleText;
        [SerializeField] private TMP_Text remainingText;
        [SerializeField] private TMP_Text bladderText;
        [SerializeField] private TMP_Text stressText;
        [SerializeField] private RectTransform bladderFill;
        [SerializeField] private RectTransform stressFill;
        private StaffController staff;
        private StaffController.Role displayedRole;
        private UnityEngine.Localization.Locale displayedLocale;
        private int displayedBladder = -1;
        private int displayedStress = -1;
        private int displayedSeconds = -1;

        public void Bind(StaffController value)
        {
            staff = value;
            displayedRole = value.currentRole;
            portrait.Show(value);
            RefreshRole();
            Refresh();
        }

        public void Refresh()
        {
            if (staff == null) return;
            nameText.text = staff.characterName;
            if (staff.currentRole != displayedRole)
            {
                displayedRole = staff.currentRole;
                portrait.Show(staff);
                RefreshRole();
            }
            var locale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
            if (locale != displayedLocale)
            {
                displayedLocale = locale;
                displayedBladder = displayedStress = displayedSeconds = -1;
            }
            SetBar(bladderFill, bladderText, "shift.bladder", staff.bladder, ref displayedBladder);
            SetBar(stressFill, stressText, "shift.stress", staff.GetCurrentStress(), ref displayedStress);
            var clock = TimeManager.Instance;
            float remaining = clock != null ? StaffScheduleMath.RemainingToday(clock.mainCalendarDay?.periodSettings,
                staff.WorkShiftMask, clock.GetCurrentPeriodType(), clock.GetPeriodTimer()) : 0;
            int seconds = Mathf.CeilToInt(remaining);
            if (displayedSeconds == seconds) return;
            displayedSeconds = seconds;
            remainingText.text = seconds > 0 ? GameText.Get("shift.remaining", $"{seconds / 60:00}:{seconds % 60:00}") : GameText.Get("shift.finishing");
        }

        private static void SetBar(RectTransform fill, TMP_Text text, string key, float value, ref int displayedValue)
        {
            value = Mathf.Clamp(value, 0, 100);
            fill.anchorMax = new Vector2(value / 100f, 1f);
            int rounded = Mathf.RoundToInt(value);
            if (displayedValue == rounded) return;
            displayedValue = rounded;
            text.text = GameText.Get(key, rounded);
        }

        private void RefreshRole()
        {
            var label = roleText.GetComponent<LocalizedText>() ?? roleText.gameObject.AddComponent<LocalizedText>();
            label.SetReference(GameText.Table, StaffRoleNames.GetKey(staff.currentRole));
            Color color = roleColors != null ? roleColors.GetColorForRole(staff.currentRole, Color.gray) : Color.gray;
            roleColor.color = color;
            portraitFrame.color = color;
        }
    }
}

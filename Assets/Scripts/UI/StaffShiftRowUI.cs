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
            SetBar(bladderFill, bladderText, "Туалет", staff.bladder);
            SetBar(stressFill, stressText, "Стресс", staff.GetCurrentStress());
            var clock = TimeManager.Instance;
            float remaining = clock != null ? StaffScheduleMath.RemainingToday(clock.mainCalendarDay?.periodSettings,
                staff.WorkShiftMask, clock.GetCurrentPeriodType(), clock.GetPeriodTimer()) : 0;
            int seconds = Mathf.CeilToInt(remaining);
            remainingText.text = seconds > 0 ? $"Сегодня ещё {seconds / 60:00}:{seconds % 60:00}" : "Завершает работу";
        }

        private static void SetBar(RectTransform fill, TMP_Text text, string label, float value)
        {
            value = Mathf.Clamp(value, 0, 100);
            fill.anchorMax = new Vector2(value / 100f, 1f);
            text.text = $"{label} {Mathf.RoundToInt(value)}%";
        }

        private void RefreshRole()
        {
            roleText.text = StaffRoleNames.GetDisplayName(staff.currentRole);
            Color color = roleColors != null ? roleColors.GetColorForRole(staff.currentRole, Color.gray) : Color.gray;
            roleColor.color = color;
            portraitFrame.color = color;
        }
    }
}

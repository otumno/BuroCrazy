using System.Collections.Generic;
using Managers;
using TMPro;
using UnityEngine;

namespace UI
{
    public class StaffShiftHUD : MonoBehaviour
    {
        [SerializeField] private StaffShiftRowUI rowTemplate;
        [SerializeField] private Transform content;
        [SerializeField] private TMP_Text title;
        [SerializeField] private GameObject emptyLabel;
        private readonly Dictionary<StaffController, StaffShiftRowUI> rows = new();
        private readonly List<StaffController> roster = new();
        private readonly List<StaffController> removed = new();
        private float nextRefresh;

        private void OnEnable() => nextRefresh = 0;

        private void Update()
        {
            // Unscaled refresh keeps the HUD accurate when the player opens a paused menu.
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .25f;
            roster.Clear();
            if (HiringManager.Instance != null)
                foreach (var staff in HiringManager.Instance.AllStaff)
                    if (staff != null && staff.currentRole != StaffController.Role.Director && staff.ShiftSequence > 0
                        && staff.gameObject.activeInHierarchy && staff.IsOnDuty() && !roster.Contains(staff))
                        roster.Add(staff);
            roster.Sort((a, b) => a.ShiftSequence.CompareTo(b.ShiftSequence));

            removed.Clear();
            foreach (var entry in rows)
                if (!roster.Contains(entry.Key)) removed.Add(entry.Key);
            foreach (var staff in removed)
            {
                if (rows[staff] != null) Destroy(rows[staff].gameObject);
                rows.Remove(staff);
            }
            for (int i = 0; i < roster.Count; i++)
            {
                var staff = roster[i];
                if (!rows.TryGetValue(staff, out var row))
                {
                    row = Instantiate(rowTemplate, content);
                    row.gameObject.SetActive(true);
                    row.Bind(staff);
                    rows.Add(staff, row);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[StaffHUDTrace] Добавлен {staff.characterName}, выход={staff.ShiftSequence}");
#endif
                }
                row.transform.SetSiblingIndex(i);
                row.Refresh();
            }
            title.text = $"На смене · {roster.Count}";
            emptyLabel.SetActive(roster.Count == 0);
        }
    }
}

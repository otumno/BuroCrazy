using System.Collections.Generic;
using UnityEngine;
using Managers;
using Managers.Teletype;

namespace Characters
{
    public static class StaffScheduleExtensions
    {
        public static void UpdateSchedule(this StaffController staff)
        {
            if (staff == null) return;

            // Проверяем есть ли поле shiftStartTime, если нет - создаем
            var shiftStartField = typeof(StaffController).GetField("shiftStartTime",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (shiftStartField == null)
            {
                shiftStartField = typeof(StaffController).GetField("shiftStartTime", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            }
            
            var hiringMgr = HiringManager.Instance;
            if (hiringMgr != null && shiftStartField != null)
            {
                var shiftDurationField = typeof(HiringManager).GetField("shiftDuration", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (shiftDurationField != null)
                {
                    float shiftDuration = (float)shiftDurationField.GetValue(hiringMgr);
                    var breakStartField = typeof(StaffController).GetField("breakStartTime", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (breakStartField != null)
                    {
                        breakStartField.SetValue(staff, shiftDuration / 2f);
                    }
                }
            }
        }

        public static void CalculateArrivalTime(this StaffController staff)
        {
            if (staff == null) return;
            
            float punctuality = GetPrivateField<float>(staff, "punctuality");
            float maxLateness = 30f;
            
            // 1. Проверка на БОЛЬНИЧНЫЙ (например, 3% шанс, уменьшается при высокой пунктуальности/морали)
            float sickChance = 0.03f * (1f - punctuality * 0.5f);
            if (Random.value < sickChance)
            {
                int currentSickDays = GetPrivateField<int>(staff, "sickDaysCount");
                SetPrivateField(staff, "sickDaysCount", currentSickDays + 1);
                
                // Устанавливаем маркер, что сегодня он не придет (огромное опоздание)
                SetPrivateField(staff, "currentLateness", -1f); 
                return;
            }

            // 2. Расчет ОПОЗДАНИЯ (в игровых тиках)
            float lateness = 0f;
            if (punctuality < 1f && Random.value >= punctuality)
            {
                // Адекватные опоздания: от 3 до 15 игровых тиков
                lateness = Random.Range(3f, 15f);
                
                int currentLatenessCount = GetPrivateField<int>(staff, "totalLatenessCount");
                SetPrivateField(staff, "totalLatenessCount", currentLatenessCount + 1);
            }
            
            SetPrivateField(staff, "currentLateness", lateness);
        }

        public static float GetLateness(this StaffController staff)
        {
            return GetPrivateField<float>(staff, "currentLateness");
        }

        public static void SetLateness(this StaffController staff, float value)
        {
            SetPrivateField(staff, "currentLateness", value);
        }

        public static bool HasArrivedToday(this StaffController staff)
        {
            return GetPrivateField<bool>(staff, "hasArrivedToday");
        }

        public static void SetArrivedToday(this StaffController staff, bool value)
        {
            SetPrivateField(staff, "hasArrivedToday", value);
        }

        public static void ResetDailySchedule(this StaffController staff)
        {
            SetPrivateField(staff, "hasArrivedToday", false);
            SetPrivateField(staff, "hasLeftToday", false);
            SetPrivateField(staff, "currentLateness", 0f);
            SetPrivateField(staff, "currentEarlyLeave", 0f);
            SetPrivateField(staff, "HasTakenBreakToday", false);
        }

        public static void UpdateBreakLogic(this StaffController staff)
        {
            if (staff == null) return;
            
            var hasLunchBreakField = typeof(StaffController).GetField("hasLunchBreak", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (hasLunchBreakField == null)
            {
                hasLunchBreakField = typeof(StaffController).GetField("hasLunchBreak", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            }
            
            if (hasLunchBreakField != null)
            {
                bool hasLunchBreak = (bool)hasLunchBreakField.GetValue(staff);
                if (!hasLunchBreak) return;
            }
            else
            {
                return; // Поле не найдено
            }
            
            bool isOnBreak = staff.IsOnBreak();
            if (isOnBreak) return;

            var timeMgr = TimeManager.Instance;
            var hiringMgr = HiringManager.Instance;
            
            if (hiringMgr == null) return;

            float shiftDuration = 480f;
            var shiftDurationField = typeof(HiringManager).GetField("shiftDuration", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (shiftDurationField != null)
            {
                shiftDuration = (float)shiftDurationField.GetValue(hiringMgr);
            }

            var shiftStartField = typeof(StaffController).GetField("shiftStartTime", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float shiftStartTime = 0f;
            if (shiftStartField != null)
            {
                shiftStartTime = (float)shiftStartField.GetValue(staff);
            }
            
            float currentTime = Time.time;
            float timeInShift = currentTime - shiftStartTime;
            float lunchTime = shiftDuration / 2f;

            var hasTakenBreakField = typeof(StaffController).GetField("HasTakenBreakToday", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            bool hasTakenBreak = false;
            if (hasTakenBreakField != null)
            {
                hasTakenBreak = (bool)hasTakenBreakField.GetValue(staff);
            }
            
            if (timeInShift >= lunchTime && !hasTakenBreak)
            {
                var breakDurationField = typeof(StaffController).GetField("breakDuration", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                float breakDuration = 900f;
                if (breakDurationField != null)
                {
                    breakDuration = (float)breakDurationField.GetValue(staff);
                }
                
                staff.GoToBreak(breakDuration);
                
                if (hasTakenBreakField != null)
                {
                    hasTakenBreakField.SetValue(staff, true);
                }
            }
        }

        public static void EndShift(this StaffController staff)
        {
            if (staff == null) return;
            
            var thoughtBubble = staff.GetComponent<ThoughtBubbleController>();
            if (thoughtBubble != null)
            {
                thoughtBubble.ShowPriorityMessage("Домой...", 2f, Color.white);
            }
            
            float currentLateness = GetPrivateField<float>(staff, "currentLateness");
            if (currentLateness > 10f)
            {
                if (thoughtBubble != null)
                    thoughtBubble.ShowPriorityMessage("Опоздал... Извините.", 2f, Color.yellow);
            }
            
            SetPrivateField(staff, "hasLeftToday", true);
        }

        public static void StartShift(this StaffController staff)
        {
            if (staff == null) return;

            staff.UpdateSchedule();

            var thoughtBubble = staff.GetComponent<ThoughtBubbleController>();
            if (thoughtBubble != null)
            {
                thoughtBubble.ShowPriorityMessage("На работу!", 2f, Color.white);
            }

            TeletypeManager.Instance?.LogStaffWork(staff.characterName, staff.role.ToString(), isStartShift: true);

            SetPrivateField(staff, "hasArrivedToday", false);
            SetPrivateField(staff, "hasLeftToday", false);
            SetPrivateField(staff, "currentLateness", 0f);
            SetPrivateField(staff, "currentEarlyLeave", 0f);
            SetPrivateField(staff, "HasTakenBreakToday", false);

            staff.CalculateArrivalTime();
        }

        private static T GetPrivateField<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public);
            
            if (field != null)
            {
                return (T)field.GetValue(obj);
            }
            
            return default(T);
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public);
            
            if (field != null)
            {
                field.SetValue(obj, value);
            }
        }
    }
}

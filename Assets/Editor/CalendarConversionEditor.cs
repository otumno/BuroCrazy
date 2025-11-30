using System.IO;
using System.Linq;
using Data.Calendar;
using Managers; // Нужно для доступа к DayPeriodManager
using Scriptables;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(CalendarDayEditor))]
    public class CalendarConversionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var spawner = (CalendarDayEditor)target;

            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Утилиты Конвертации", EditorStyles.boldLabel);

            if (GUILayout.Button("Конвертировать 'Periods' в Ассет Календаря"))
            {
                if (EditorUtility.DisplayDialog("Подтверждение конвертации",
                    "Это действие создаст новый ассет GameCalendar на основе текущих настроек 'Periods'. Старый массив 'Periods' не будет удален. Вы уверены?",
                    "Да, конвертировать", "Отмена"))
                {
                    ConvertPeriodsToCalendar(spawner);
                }
            }
        }

        private void ConvertPeriodsToCalendar(CalendarDayEditor dayEditor)
        {
            if (dayEditor.Periods == null || dayEditor.Periods.Length == 0)
            {
                EditorUtility.DisplayDialog("Ошибка", "Массив 'Periods' пуст. Нечего конвертировать.", "OK");
                return;
            }

            CalendarDay newCalendarDay = ScriptableObject.CreateInstance<CalendarDay>();
            newCalendarDay.periodSettings = new System.Collections.Generic.List<PeriodSettings>();

            foreach (var oldPeriod in dayEditor.Periods)
            {
                var newPeriodSetting = new PeriodSettings
                {
                    PeriodType = oldPeriod.PeriodType,
                    durationInSeconds = oldPeriod.durationInSeconds,
                    
                    clientCount = new AnimationCurve(new Keyframe(1, oldPeriod.crowdSpawnCount)), 
                    spawnRate = new AnimationCurve(new Keyframe(1, oldPeriod.spawnRate)),
                    spawnBatchSize = new AnimationCurve(new Keyframe(1, oldPeriod.spawnBatchSize)),
                    crowdSpawnCount = new AnimationCurve(new Keyframe(1, oldPeriod.crowdSpawnCount)),
                    numberOfCrowdsToSpawn = new AnimationCurve(new Keyframe(1, oldPeriod.numberOfCrowdsToSpawn)),
                
                    lightingSettings = new LightingPreset
                    {
                        lightColor = oldPeriod.lightingSettings.lightColor,
                        lightIntensity = oldPeriod.lightingSettings.lightIntensity
                    },
                    panelColor = oldPeriod.panelColor,
                    
                    lightsToEnableNames = oldPeriod.lightsToEnable.Where(l => l != null).Select(l => l.name).ToList()
                };
                newCalendarDay.periodSettings.Add(newPeriodSetting);
            }
        
            string path = "Assets/Data/Calendar";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + "/MainGameCalendar.asset");

            AssetDatabase.CreateAsset(newCalendarDay, assetPathAndName);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // --- ИСПРАВЛЕНИЕ ЗДЕСЬ ---
            // Пытаемся найти DayPeriodManager вместо ClientSpawner
            var dayManager = FindFirstObjectByType<DayPeriodManager>();
            
            if (dayManager != null)
            {
                dayManager.mainCalendarDay = newCalendarDay;
                EditorUtility.SetDirty(dayManager);
                Debug.Log($"<color=green>Календарь назначен в DayPeriodManager.</color>");
            }
            else
            {
                Debug.LogWarning("DayPeriodManager не найден на сцене. Пожалуйста, назначьте созданный календарь вручную.");
            }
            // -------------------------

            EditorUtility.DisplayDialog("Успех", $"Конвертация завершена! Создан новый ассет:\n{assetPathAndName}", "Отлично!");
        }
    }
}
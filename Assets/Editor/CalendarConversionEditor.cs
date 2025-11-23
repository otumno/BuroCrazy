using System.IO;
using System.Linq;
using Data.Calendar;
using Managers;
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
                EditorUtility.DisplayDialog("Ошибка", "Массив 'Periods' в ClientSpawner пуст. Нечего конвертировать.", "OK");
                return;
            }

            CalendarDay newCalendarDay = ScriptableObject.CreateInstance<CalendarDay>();
            // Мы больше не используем DailyPlan и 30-дневную структуру
            newCalendarDay.periodSettings = new System.Collections.Generic.List<PeriodSettings>();

            // Напрямую конвертируем каждый старый период в новый формат с кривыми
            foreach (var oldPeriod in dayEditor.Periods)
            {
                var newPeriodSetting = new PeriodSettings
                {
                    // period
                    PeriodType = oldPeriod.PeriodType,
                
                    // duration
                    durationInSeconds = oldPeriod.durationInSeconds,
                    
                    // spawn curves
                    clientCount = new AnimationCurve(new Keyframe(1, oldPeriod.crowdSpawnCount)), // Предполагаем, что clientCount - это основной параметр
                    spawnRate = new AnimationCurve(new Keyframe(1, oldPeriod.spawnRate)),
                    spawnBatchSize = new AnimationCurve(new Keyframe(1, oldPeriod.spawnBatchSize)),
                    crowdSpawnCount = new AnimationCurve(new Keyframe(1, oldPeriod.crowdSpawnCount)),
                    numberOfCrowdsToSpawn = new AnimationCurve(new Keyframe(1, oldPeriod.numberOfCrowdsToSpawn)),
                
                    // light
                    lightingSettings = new LightingPreset
                    {
                        lightColor = oldPeriod.lightingSettings.lightColor,
                        lightIntensity = oldPeriod.lightingSettings.lightIntensity
                    },
                    panelColor = oldPeriod.panelColor,
                    
                    // go names save
                    lightsToEnableNames = oldPeriod.lightsToEnable.Where(l => l != null).Select(l => l.name).ToList()
                };
                // Добавляем настроенный период в корневой список
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

            var clientSpawners = FindObjectsByType<ClientSpawner>(FindObjectsSortMode.None);
            var spawner = clientSpawners[0];
            spawner.mainCalendarDay = newCalendarDay;
            EditorUtility.SetDirty(spawner);

            EditorUtility.DisplayDialog("Успех", $"Конвертация завершена! Создан и назначен новый ассет:\n{assetPathAndName}", "Отлично!");
            Debug.Log($"<color=green>Конвертация завершена! Создан новый ассет '{assetPathAndName}' и назначен в ClientSpawner.</color>");
        }
    }
}
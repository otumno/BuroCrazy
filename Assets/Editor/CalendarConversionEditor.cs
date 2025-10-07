using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

[CustomEditor(typeof(ClientSpawner))]
public class CalendarConversionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        ClientSpawner spawner = (ClientSpawner)target;

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

    private void ConvertPeriodsToCalendar(ClientSpawner spawner)
    {
        if (spawner.periods == null || spawner.periods.Length == 0)
        {
            EditorUtility.DisplayDialog("Ошибка", "Массив 'Periods' в ClientSpawner пуст. Нечего конвертировать.", "OK");
            return;
        }

        GameCalendar newCalendar = ScriptableObject.CreateInstance<GameCalendar>();
        // Мы больше не используем DailyPlan и 30-дневную структуру
        newCalendar.periodSettings = new System.Collections.Generic.List<PeriodSettings>();

        // Напрямую конвертируем каждый старый период в новый формат с кривыми
        foreach (var oldPeriod in spawner.periods)
        {
            var newPeriodSetting = new PeriodSettings
            {
                periodName = oldPeriod.periodName,
                
                // Создаем кривую с одним ключом - это будет стартовое значение для дня 1.
                // Остальные дни можно будет настроить в новом редакторе.
                clientCount = new AnimationCurve(new Keyframe(1, oldPeriod.crowdSpawnCount)), // Предполагаем, что clientCount - это основной параметр
                durationInSeconds = new AnimationCurve(new Keyframe(1, oldPeriod.durationInSeconds)),
                spawnRate = new AnimationCurve(new Keyframe(1, oldPeriod.spawnRate)),
                spawnBatchSize = new AnimationCurve(new Keyframe(1, oldPeriod.spawnBatchSize)),
                crowdSpawnCount = new AnimationCurve(new Keyframe(1, oldPeriod.crowdSpawnCount)),
                numberOfCrowdsToSpawn = new AnimationCurve(new Keyframe(1, oldPeriod.numberOfCrowdsToSpawn)),
                
                // Копируем остальные настройки как есть
                lightingSettings = new LightingPreset
                {
                    lightColor = oldPeriod.lightingSettings.lightColor,
                    lightIntensity = oldPeriod.lightingSettings.lightIntensity
                },
                panelColor = oldPeriod.panelColor,
                lightsToEnableNames = oldPeriod.lightsToEnable.Where(l => l != null).Select(l => l.name).ToList()
            };
            // Добавляем настроенный период в корневой список
            newCalendar.periodSettings.Add(newPeriodSetting);
        }
        
        string path = "Assets/Data/Calendar";
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + "/MainGameCalendar.asset");

        AssetDatabase.CreateAsset(newCalendar, assetPathAndName);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        spawner.mainCalendar = newCalendar;
        EditorUtility.SetDirty(spawner);

        EditorUtility.DisplayDialog("Успех", $"Конвертация завершена! Создан и назначен новый ассет:\n{assetPathAndName}", "Отлично!");
        Debug.Log($"<color=green>Конвертация завершена! Создан новый ассет '{assetPathAndName}' и назначен в ClientSpawner.</color>");
    }
}
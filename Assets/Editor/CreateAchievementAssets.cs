// Assets/Editor/CreateAchievementAssets.cs
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CreateAchievementAssets
{
    private const string TargetFolder = "Assets/Thoughts/Achivments";

    private struct Entry
    {
        public string id;
        public string displayName;
        public string description;
        public Entry(string id, string displayName, string description)
        {
            this.id = id;
            this.displayName = displayName;
            this.description = description;
        }
    }

    [MenuItem("Bureau/Create Achievement Assets")]
    public static void CreateAll()
    {
        var entries = new List<Entry>
        {
            new Entry("Achv_DocumentStory",         "Документ",                 "История о документе, который впервые получил одобрение Директора."),
            new Entry("Achv_LostClientStory",       "Заблудившийся клиент",     "Стажёр впервые помог растерянному посетителю найти дорогу."),
            new Entry("Achv_CleanerStory",          "Уборщица",                 "Уборщик впервые навёл порядок в кабинете, убрав мусор."),
            new Entry("Achv_DirectorStory",         "Директор",                 "Директор впервые занял своё законное место за рабочим столом."),
            new Entry("Achv_GuardStory",            "Охранник",                 "Охранник впервые задержал вора или утихомирил нарушителя."),
            new Entry("Achv_GrandmaClientStory",    "Клиентка-бабушка",         "Бабушка-клиентка впервые была успешно обслужена."),
            new Entry("Achv_InternStory",           "Стажёр",                   "Стажёр впервые подменил клерка на его рабочем месте."),
            new Entry("Achv_ArchivistStory",        "Архивариус",               "Архивариус впервые нашёл и доставил затребованный документ."),
            new Entry("Achv_TheFixerStory",         "Пролаза",                  "Клиент-пролаза впервые был успешно обслужен."),
            new Entry("Achv_ExperiencedClerkStory", "Опытный клерк",            "Впервые в бюро появился клерк — будь то найм или повышение стажёра."),
        };

        if (!AssetDatabase.IsValidFolder(TargetFolder))
        {
            Directory.CreateDirectory(TargetFolder);
            AssetDatabase.Refresh();
        }

        int created = 0;
        int skipped = 0;

        foreach (var e in entries)
        {
            string assetPath = $"{TargetFolder}/{e.id}.asset";
            if (AssetDatabase.LoadAssetAtPath<AchievementData>(assetPath) != null)
            {
                skipped++;
                continue;
            }

            var asset = ScriptableObject.CreateInstance<AchievementData>();
            asset.achievementID = e.id;
            asset.displayName   = e.displayName;
            asset.description   = e.description;
            // Иконки и страницы оставляем пустыми — заполнятся вручную.
            // comicPages тоже пуст — игрок сам заполнит.

            AssetDatabase.CreateAsset(asset, assetPath);
            EditorUtility.SetDirty(asset);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CreateAchievementAssets] Готово. Создано: {created}, уже существовало: {skipped}.");
    }
}
#endif

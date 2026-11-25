using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;

// Idempotent import of the reviewed translations. Run through Unity CLI, outside Play Mode.
public static class SyncGameplayLocalization
{
    [DataContract] private class Translation
    {
        [DataMember] public string key;
        [DataMember] public string ru;
        [DataMember] public string en;
        [DataMember] public bool smart;
        [DataMember] public string executor;
    }

    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        // Pipeline exports two Newtonsoft versions; JsonUtility doesn't register dynamically compiled DTOs.
        Translation[] entries;
        using (var input = File.OpenRead("Tools/Localization/Gameplay.json"))
            entries = (Translation[])new DataContractJsonSerializer(typeof(Translation[])).ReadObject(input);
        var locales = LocalizationEditorSettings.GetLocales().Where(l => l.Identifier.Code == "ru" || l.Identifier.Code == "en").ToList();
        if (locales.Count != 2) throw new InvalidOperationException("Expected existing ru and en locales.");
        if (!AssetDatabase.IsValidFolder("Assets/Localization/Gameplay")) AssetDatabase.CreateFolder("Assets/Localization", "Gameplay");
        var collection = LocalizationEditorSettings.GetStringTableCollection("Gameplay") ??
            LocalizationEditorSettings.CreateStringTableCollection("Gameplay", "Assets/Localization/Gameplay", locales);
        foreach (var locale in locales)
        {
            var table = (StringTable)(collection.GetTable(locale.Identifier) ?? collection.AddNewTable(locale.Identifier));
            foreach (var entry in entries)
            {
                string value = locale.Identifier.Code == "ru" ? entry.ru : entry.en;
                if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Empty translation: " + entry.key);
                table.AddEntry(entry.key, value).IsSmart = entry.smart;
            }
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssetIfDirty(table);
        }
        SaveCollection(collection);

        foreach (var guid in AssetDatabase.FindAssets("t:StaffAction", new[] { "Assets" }))
        {
            var action = AssetDatabase.LoadAssetAtPath<StaffAction>(AssetDatabase.GUIDToAssetPath(guid));
            var type = action.GetExecutorType();
            var entry = type != null ? entries.FirstOrDefault(e => e.executor == type.Name) : null;
            if (entry == null || action.localizedNameKey == entry.key) continue;
            action.localizedNameKey = entry.key;
            EditorUtility.SetDirty(action);
            AssetDatabase.SaveAssetIfDirty(action);
        }

        // The existing temporary-hire label had an English table but no Russian counterpart.
        var main = LocalizationEditorSettings.GetStringTableCollection("Localization_Table_Main");
        if (main != null)
        {
            var russian = (StringTable)(main.GetTable(new LocaleIdentifier("ru")) ?? main.AddNewTable(new LocaleIdentifier("ru")));
            russian.AddEntry("hiring.temporary", "ВРЕМЕННЫЙ");
            EditorUtility.SetDirty(russian);
            AssetDatabase.SaveAssetIfDirty(russian);
            SaveCollection(main);
        }

        const string prefabPath = "Assets/Prefabs/DirectorUi/BookkeepingPanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var panel = root.GetComponent<UI.Bookkeeping.BookkeepingPanelUI>();
            var button = new SerializedObject(panel).FindProperty("nextButton").objectReferenceValue as UnityEngine.UI.Button;
            var label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (label == null) throw new InvalidOperationException("Missing Next label.");
            var binding = label.GetComponent<LocalizeStringEvent>() ?? label.gameObject.AddComponent<LocalizeStringEvent>();
            binding.StringReference.SetReference("Gameplay", "ui.common.next");
            for (int i = binding.OnUpdateString.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(binding.OnUpdateString, i);
            UnityEventTools.AddPersistentListener(binding.OnUpdateString,
                (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), label, "set_text"));
            binding.OnUpdateString.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        // Table creation changes Addressables groups. Persist only those assets, not unrelated open scene/font edits.
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null)
        {
            foreach (var group in settings.groups.Where(g => g != null))
            {
                AssetDatabase.SaveAssetIfDirty(group);
                foreach (var schema in group.Schemas) AssetDatabase.SaveAssetIfDirty(schema);
            }
            AssetDatabase.SaveAssetIfDirty(settings);
        }
        return "Gameplay: " + entries.Length + " keys in ru/en; preloaded; Next TMP binding; existing hiring Russian table added.";
    }

    private static void SaveCollection(StringTableCollection collection)
    {
        collection.SetPreloadTableFlag(true);
        EditorUtility.SetDirty(collection.SharedData);
        EditorUtility.SetDirty(collection);
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(typeof(SyncGameplayLocalization), collection);
        AssetDatabase.SaveAssetIfDirty(collection.SharedData);
        AssetDatabase.SaveAssetIfDirty(collection);
        foreach (var table in collection.StringTables) AssetDatabase.SaveAssetIfDirty(table);
    }
}

using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

public static class GameplayLocalizationCheck
{
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        var collection = LocalizationEditorSettings.GetStringTableCollection("Gameplay");
        Require(collection != null, "Gameplay collection missing.");
        foreach (var table in collection.StringTables)
            foreach (var key in collection.SharedData.Entries)
                Require(!string.IsNullOrWhiteSpace(table.GetEntry(key.Id)?.Value), "Empty " + table.LocaleIdentifier + ": " + key.Key);
        Require(collection.StringTables.Count == 2, "Expected ru/en tables.");
        var original = LocalizationSettings.SelectedLocale;
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DirectorUi/BookkeepingPanel.prefab");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var binding = root.GetComponentsInChildren<LocalizeStringEvent>(true)
                .Single(e => e.StringReference.TableEntryReference.Key == "ui.common.next");
            var text = binding.GetComponent<TMP_Text>();
            Require(binding.OnUpdateString.GetPersistentTarget(0) == text && binding.OnUpdateString.GetPersistentMethodName(0) == "set_text",
                "TMP event target invalid.");
            Require(binding.OnUpdateString.GetPersistentListenerState(0) == UnityEngine.Events.UnityEventCallState.EditorAndRuntime,
                "TMP event not enabled for editor preview.");
            root.SetActive(true);
            binding.gameObject.SetActive(true);
            foreach (string code in new[] { "ru", "en" })
            {
                var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
                Require(locale != null, "Locale missing: " + code);
                LocalizationSettings.SelectedLocale = locale;
                var expected = code == "ru" ? "Далее" : "Next";
                Require(UI.GameText.Get("ui.common.next") == expected, "SelectedLocale not used by GameText.");
                binding.StringReference.LocaleOverride = locale;
                var resolved = binding.StringReference.GetLocalizedString();
                Require(resolved == expected, "Binding resolves wrong locale.");
                binding.OnUpdateString.Invoke(resolved);
                Require(text.text == expected, "TMP setter did not fire.");
                foreach (var key in collection.SharedData.Entries)
                {
                    var arguments = key.Key == "ledger.player_share" ? new object[] { "Service", .1f } : new object[] { "Alex", "Desk", 3.5f };
                    var value = LocalizationSettings.StringDatabase.GetLocalizedString("Gameplay", key.Key, arguments, locale);
                    Require(!string.IsNullOrEmpty(value) && !value.Contains("No translation found"), "Runtime lookup failed: " + key.Key);
                }
                var due = UI.GameText.Get("staff.cashier.payment_due", 125);
                Require(due.Contains("$125") && !due.Contains("{0}"), "Smart amount not formatted.");
                var hire = LocalizationSettings.StringDatabase.GetLocalizedString("Localization_Table_Main", "hiring.temporary");
                Require(hire == (code == "ru" ? "ВРЕМЕННЫЙ" : "TEMPORARY"), "Temporary hire translation missing.");
            }
            return "PASS: all Gameplay entries nonempty in ru/en; runtime locale lookup and smart values; saved Next TMP binding.";
        }
        finally
        {
            LocalizationSettings.SelectedLocale = original;
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}

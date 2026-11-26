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
        using (var input = System.IO.File.OpenRead("Tools/Localization/Gameplay.json"))
        using (var reader = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(input, System.Xml.XmlDictionaryReaderQuotas.Max))
        {
            var rows = System.Xml.Linq.XDocument.Load(reader).Root.Elements().ToArray();
            Require(rows.Length == collection.SharedData.Entries.Count, "Source and shared key counts differ.");
            foreach (var row in rows)
                foreach (var table in collection.StringTables)
                {
                    var entry = table.GetEntry((string)row.Element("key"));
                    Require(entry != null && entry.Value == (string)row.Element(table.LocaleIdentifier.Code) &&
                        entry.IsSmart == ((bool?)row.Element("smart") ?? false), "Translation differs from source: " + row.Element("key"));
                }
        }
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
                    var arguments = key.Key == "ledger.player_share" ? new object[] { "Service", .1f } : new object[] { "Alex", "Desk", 3.5f, 4 };
                    var value = LocalizationSettings.StringDatabase.GetLocalizedString("Gameplay", key.Key, arguments, locale);
                    Require(!string.IsNullOrEmpty(value) && !value.Contains("No translation found"), "Runtime lookup failed: " + key.Key);
                }
                var tacticsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DirectorUi/ActionConfigPopup.prefab");
                var tacticsKeys = tacticsPrefab.GetComponentsInChildren<UI.LocalizedText>(true)
                    .Select(b => new SerializedObject(b).FindProperty("_localizedString.m_TableEntryReference.m_Key").stringValue).ToArray();
                foreach (var key in new[] { "tactics.save", "panel.cancel", "panel.shifts", "tactics.assignment", "panel.menu" })
                    Require(tacticsKeys.Contains(key), "Missing tactics prefab binding: " + key);
                foreach (var guid in AssetDatabase.FindAssets("t:StaffAction", new[] { "Assets" }))
                {
                    var action = AssetDatabase.LoadAssetAtPath<StaffAction>(AssetDatabase.GUIDToAssetPath(guid));
                    if (action == null) continue;
                    var key = string.IsNullOrWhiteSpace(action.localizedNameKey) ? "action.asset." + action.name : action.localizedNameKey;
                    var value = collection.StringTables.Single(t => t.LocaleIdentifier.Code == code).GetEntry(key);
                    Require(value != null && action.LocalizedDisplayName == value.Value, "Untranslated staff action: " + key);
                }
                var bio = new ResumeBio { education = 0, hobby = 2, quirk = 3 };
                Require(bio.Localize().Contains(code == "ru" ? "редких скрепок" : "rare paper clips"), "Bio changed meaning across locales.");
                var skills = new StaffController.CharacterSkillsWrapper { pedantry = .25f };
                Require(skills.GetSkillShortText(Utilities.SkillType.Pedantry) == (code == "ru" ? "Педантичность: 25%" : "Attention to detail: 25%"),
                    "Staff skill name or value lost.");
                var orders = AssetDatabase.LoadAssetAtPath<StaffOrders.StaffOrderCatalog>("Assets/Data/StaffOrders/StaffOrderCatalog.asset");
                foreach (var order in orders.Orders.Where(o => !string.IsNullOrEmpty(o.LabelKey)))
                {
                    var label = UI.GameText.Get(order.LabelKey);
                    Require(code == "ru" ? label == order.Label : !string.IsNullOrWhiteSpace(label) && label != order.Label,
                        "Wrong order label: " + order.LabelKey);
                }
                Require(UI.GameText.Get("shift.title", 2) == (code == "ru" ? "На смене · 2" : "On duty · 2"), "Wrong roster count.");
                Require(UI.GameText.Get("shift.stress", 75) == (code == "ru" ? "Стресс 75%" : "Stress 75%"), "Wrong stress label.");
                Require(UI.GameText.Get("shift.remaining", "03:07").Contains("03:07"), "Shift time lost its padding.");
                Require(UI.GameText.Get("hiring.temp_cost", 60, 40, 1, 20).Contains("1 × $20"), "Temporary quote lost its actual shift.");
                Require(UI.GameText.Get("staff.accounts.covered", 37) == (code == "ru" ? "Прикрыл на 37₽" : "Covered ₽37"),
                    "Accountant amount or currency incorrect.");
                Require(UI.GameText.Get("staff.guide.to_reception") == (code == "ru" ? "Вам в приёмную директора!" : "Head to the director's reception!"),
                    "Intern gave directions in the wrong language.");
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

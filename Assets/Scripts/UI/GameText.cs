using UnityEngine.Localization.Settings;

namespace UI
{
    /// <summary>Semantic aliases for short, code-generated gameplay text.</summary>
    public static class GameText
    {
        public const string Table = "Gameplay";

        public static string GetAssetText(string key, string authoredText) =>
            LocalizationSettings.StringDatabase.GetTable(Table)?.GetEntry(key)?.GetLocalizedString() ?? authoredText;

        // The collection is preloaded by Unity Localization. Use the package's selected
        // locale and formatter; a separate language cache would ignore SettingsView.
        public static string Get(string key, params object[] arguments) =>
            LocalizationSettings.StringDatabase.GetLocalizedString(Table, key, arguments);
    }
}

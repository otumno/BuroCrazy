using UnityEngine;

namespace GameInitialization
{
    /// <summary>
    /// Применяет сохранённые оконные настройки (разрешение, режим, VSync) до загрузки первой сцены,
    /// чтобы экран не моргал на старте. Ключи PlayerPrefs здесь — единственный источник правды,
    /// SettingsView пишет и читает по этим же константам.
    /// </summary>
    public static class DisplaySettingsBootstrap
    {
        public const string PrefResolutionW  = "settings.resolution.width";
        public const string PrefResolutionH  = "settings.resolution.height";
        public const string PrefResolutionHz = "settings.resolution.refresh";
        public const string PrefScreenMode   = "settings.screenmode";
        public const string PrefVsync        = "settings.vsync";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            int storedVsync = PlayerPrefs.GetInt(PrefVsync, QualitySettings.vSyncCount > 0 ? 1 : 0);
            QualitySettings.vSyncCount = storedVsync > 0 ? 1 : 0;

            if (!PlayerPrefs.HasKey(PrefResolutionW) || !PlayerPrefs.HasKey(PrefResolutionH))
                return;

            int width  = PlayerPrefs.GetInt(PrefResolutionW);
            int height = PlayerPrefs.GetInt(PrefResolutionH);
            int hz     = PlayerPrefs.GetInt(PrefResolutionHz, 0);
            var mode   = (FullScreenMode)PlayerPrefs.GetInt(PrefScreenMode, (int)Screen.fullScreenMode);

            if (hz > 0)
                Screen.SetResolution(width, height, mode, new RefreshRate { numerator = (uint)hz, denominator = 1 });
            else
                Screen.SetResolution(width, height, mode);
        }
    }
}

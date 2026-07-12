using System.Collections.Generic;
using GameInitialization;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace UI.Settings
{
    public class SettingsView : MonoBehaviour
    {
        // Ключи для дисплейных настроек живут в DisplaySettingsBootstrap, чтобы бутстрап и UI читали одно и то же.
        private const string PrefResolutionW  = DisplaySettingsBootstrap.PrefResolutionW;
        private const string PrefResolutionH  = DisplaySettingsBootstrap.PrefResolutionH;
        private const string PrefResolutionHz = DisplaySettingsBootstrap.PrefResolutionHz;
        private const string PrefScreenMode   = DisplaySettingsBootstrap.PrefScreenMode;
        private const string PrefVsync        = DisplaySettingsBootstrap.PrefVsync;
        // Ключи и имена параметров живут в AudioVolumeSettings — один источник правды с AudioManager.
        private const string PrefUiVolume    = AudioVolumeSettings.PrefUiVolume;
        private const string PrefSfxVolume   = AudioVolumeSettings.PrefSfxVolume;
        private const string PrefMusicVolume = AudioVolumeSettings.PrefMusicVolume;

        [Header("UI ссылки")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private TMP_Dropdown screenModeDropdown;
        [SerializeField] private Toggle vsyncToggle;
        [SerializeField] private TMP_Dropdown languageDropdown;
        [SerializeField] private Slider uiVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;

        [Header("Поведение")]
        [Tooltip("Минимальная громкость на слайдере (нормализованная 0..1).")]
        [SerializeField, Range(0f, 1f)] private float minVolume = 0.0001f;

        private readonly List<Resolution> uniqueResolutions = new();
        private readonly List<Locale> availableLocales = new();
        private bool isApplyingFromCode;

        private void Awake()
        {
            BuildResolutions();
            BuildScreenModes();
            BuildLanguages();
        }

        private void OnEnable()
        {
            LoadValuesIntoUI();
            HookListeners(true);
            ApplyAudioFromCurrentValues();
        }

        private void OnDisable()
        {
            HookListeners(false);
        }

        private void HookListeners(bool subscribe)
        {
            if (subscribe)
            {
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
                screenModeDropdown.onValueChanged.AddListener(OnScreenModeChanged);
                vsyncToggle.onValueChanged.AddListener(OnVsyncChanged);
                languageDropdown.onValueChanged.AddListener(OnLanguageDropdownChanged);
                // Слайдеры громкости назначаются в сцене; страхуемся от null, чтобы не ронять всю панель настроек.
                uiVolumeSlider.onValueChanged.AddListener(OnUiVolumeChanged);
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            }
            else
            {
                resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
                screenModeDropdown.onValueChanged.RemoveListener(OnScreenModeChanged);
                vsyncToggle.onValueChanged.RemoveListener(OnVsyncChanged);
                languageDropdown.onValueChanged.RemoveListener(OnLanguageDropdownChanged);
                uiVolumeSlider.onValueChanged.RemoveListener(OnUiVolumeChanged);
                sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
                musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            }
        }

        // --- BUILD ---

        private void BuildResolutions()
        {
            if (resolutionDropdown == null) return;

            uniqueResolutions.Clear();
            var seen = new HashSet<(int w, int h)>();
            foreach (var res in Screen.resolutions)
            {
                if (seen.Add((res.width, res.height)))
                {
                    uniqueResolutions.Add(res);
                }
            }

            var labels = new List<string>(uniqueResolutions.Count);
            foreach (var res in uniqueResolutions)
            {
                labels.Add($"{res.width} x {res.height}");
            }

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(labels);
        }

        private void BuildScreenModes()
        {
            if (screenModeDropdown == null) return;
            screenModeDropdown.ClearOptions();
            screenModeDropdown.AddOptions(new List<string> { "Полноэкранный", "Оконный", "Окно во весь экран" });
        }

        private void BuildLanguages()
        {
            if (languageDropdown == null) return;

            availableLocales.Clear();
            availableLocales.AddRange(LocalizationSettings.AvailableLocales.Locales);

            var labels = new List<string>(availableLocales.Count);
            foreach (var locale in availableLocales)
            {
                // LocaleName уже учитывает культурную информацию (например, "Русский (Россия)").
                labels.Add(locale.LocaleName);
            }

            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(labels);
        }

        // --- LOAD INTO UI ---

        private void LoadValuesIntoUI()
        {
            isApplyingFromCode = true;

            if (resolutionDropdown != null && uniqueResolutions.Count > 0)
            {
                int storedW = PlayerPrefs.GetInt(PrefResolutionW, Screen.currentResolution.width);
                int storedH = PlayerPrefs.GetInt(PrefResolutionH, Screen.currentResolution.height);
                int idx = uniqueResolutions.FindIndex(r => r.width == storedW && r.height == storedH);
                if (idx < 0) idx = uniqueResolutions.FindIndex(r => r.width == Screen.width && r.height == Screen.height);
                if (idx < 0) idx = uniqueResolutions.Count - 1;
                resolutionDropdown.SetValueWithoutNotify(idx);
                resolutionDropdown.RefreshShownValue();
            }

            if (screenModeDropdown != null)
            {
                int storedMode = PlayerPrefs.GetInt(PrefScreenMode, (int)Screen.fullScreenMode);
                screenModeDropdown.SetValueWithoutNotify(ScreenModeIndexFromMode((FullScreenMode)storedMode));
                screenModeDropdown.RefreshShownValue();
            }

            if (vsyncToggle != null)
            {
                int storedVsync = PlayerPrefs.GetInt(PrefVsync, QualitySettings.vSyncCount > 0 ? 1 : 0);
                vsyncToggle.SetIsOnWithoutNotify(storedVsync > 0);
            }

            if (languageDropdown != null && availableLocales.Count > 0)
            {
                // Локализация сама хранит выбранную локаль в PlayerPrefs ("selected-locale-..."),
                // поэтому полагаемся на её selectedLocale, а не на собственный ключ.
                var selected = LocalizationSettings.SelectedLocale;
                int idx = selected != null ? availableLocales.IndexOf(selected) : -1;
                if (idx < 0) idx = 0;
                languageDropdown.SetValueWithoutNotify(idx);
                languageDropdown.RefreshShownValue();
            }

            uiVolumeSlider.minValue = 0f;
            uiVolumeSlider.maxValue = 1f;
            uiVolumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(PrefUiVolume, 1f));

            sfxVolumeSlider.minValue = 0f;
            sfxVolumeSlider.maxValue = 1f;
            sfxVolumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(PrefSfxVolume, 1f));

            musicVolumeSlider.minValue = 0f;
            musicVolumeSlider.maxValue = 1f;
            musicVolumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(PrefMusicVolume, 1f));

            isApplyingFromCode = false;
        }

        // --- HANDLERS ---

        private void OnResolutionChanged(int index)
        {
            if (isApplyingFromCode) return;
            if (index < 0 || index >= uniqueResolutions.Count) return;

            var res = uniqueResolutions[index];
            var mode = ScreenModeFromIndex(screenModeDropdown.value);
            Screen.SetResolution(res.width, res.height, mode);

            PlayerPrefs.SetInt(PrefResolutionW, res.width);
            PlayerPrefs.SetInt(PrefResolutionH, res.height);
            PlayerPrefs.SetInt(PrefResolutionHz, (int)res.refreshRateRatio.numerator);
            PlayerPrefs.Save();
        }

        private void OnScreenModeChanged(int index)
        {
            if (isApplyingFromCode) return;
            var mode = ScreenModeFromIndex(index);
            Screen.fullScreenMode = mode;
            PlayerPrefs.SetInt(PrefScreenMode, (int)mode);
            PlayerPrefs.Save();
        }

        private void OnVsyncChanged(bool isOn)
        {
            if (isApplyingFromCode) return;
            QualitySettings.vSyncCount = isOn ? 1 : 0;
            PlayerPrefs.SetInt(PrefVsync, isOn ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void OnLanguageDropdownChanged(int index)
        {
            if (isApplyingFromCode) return;
            if (index < 0 || index >= availableLocales.Count) return;

            // LocalizationSettings сам сохранит выбор и оповестит подписчиков OnSelectedLocaleChanged.
            LocalizationSettings.SelectedLocale = availableLocales[index];
        }

        private void OnUiVolumeChanged(float value)
        {
            if (isApplyingFromCode) return;
            PlayerPrefs.SetFloat(PrefUiVolume, value);
            PlayerPrefs.Save();
            ApplyUiVolume(value);
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (isApplyingFromCode) return;
            PlayerPrefs.SetFloat(PrefSfxVolume, value);
            PlayerPrefs.Save();
            ApplySfxVolume(value);
        }

        private void OnMusicVolumeChanged(float value)
        {
            if (isApplyingFromCode) return;
            PlayerPrefs.SetFloat(PrefMusicVolume, value);
            PlayerPrefs.Save();
            ApplyMusicVolume(value);
        }

        // --- APPLY ---

        private void ApplyAudioFromCurrentValues()
        {
            ApplyUiVolume(uiVolumeSlider.value);
            ApplySfxVolume(sfxVolumeSlider.value);
            ApplyMusicVolume(musicVolumeSlider.value);
        }

        private void ApplyUiVolume(float normalized)
        {
            AudioManager.Instance?.SetVolume(AudioVolumeSettings.ParamUi, NormalizeVolume(normalized));
        }

        private void ApplySfxVolume(float normalized)
        {
            AudioManager.Instance?.SetVolume(AudioVolumeSettings.ParamSfx, NormalizeVolume(normalized));
        }

        private void ApplyMusicVolume(float normalized)
        {
            AudioManager.Instance?.SetVolume(AudioVolumeSettings.ParamMusic, NormalizeVolume(normalized));
        }
        
        private float NormalizeVolume(float value) => Mathf.Max(minVolume, value);

        // --- MAPPING ---

        // Порядок дропдауна экранного режима: 0 = Fullscreen, 1 = Windowed, 2 = Windowed Fullscreen.
        // Соответствует FullScreenMode.ExclusiveFullScreen / Windowed / FullScreenWindow.
        private static FullScreenMode ScreenModeFromIndex(int index)
        {
            return index switch
            {
                0 => FullScreenMode.ExclusiveFullScreen,
                1 => FullScreenMode.Windowed,
                2 => FullScreenMode.FullScreenWindow,
                _ => FullScreenMode.FullScreenWindow,
            };
        }

        private static int ScreenModeIndexFromMode(FullScreenMode mode)
        {
            return mode switch
            {
                FullScreenMode.ExclusiveFullScreen => 0,
                FullScreenMode.Windowed            => 1,
                FullScreenMode.FullScreenWindow    => 2,
                FullScreenMode.MaximizedWindow     => 2,
                _ => 2,
            };
        }
    }
}

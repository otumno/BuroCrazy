using Managers;
using UnityEngine;

namespace GameInitialization
{
    /// <summary>
    /// Единый источник правды для громкости: ключи PlayerPrefs и имена exposed-параметров микшера.
    /// И SettingsView (запись из UI), и AudioManager (применение на старте) читают отсюда, чтобы
    /// имена параметров не расходились с MainMixer.mixer (раньше это уже приводило к неработающим слайдерам).
    /// Три независимые группы: UI (2D-звуки интерфейса), SFX (3D-звуки NPC), Music (музыка).
    /// Общего Master-слайдера нет намеренно — этих трёх достаточно для управления всем звуком.
    /// </summary>
    public static class AudioVolumeSettings
    {
        public const string PrefUiVolume    = "settings.volume.ui";
        public const string PrefSfxVolume   = "settings.volume.sfx";
        public const string PrefMusicVolume = "settings.volume.music";

        // Имена ДОЛЖНЫ совпадать с exposed-параметрами MainMixer.mixer (UI / SFX / Music).
        public const string ParamUi    = "UI";
        public const string ParamSfx   = "SFX";
        public const string ParamMusic = "Music";

        private const float DefaultVolume = 1f;

        public static void Apply(AudioManager audioManager)
        {
            if (audioManager == null)
            {
                return;
            }

            // Конвертацию 0..1 -> дБ делает сам AudioManager.SetVolume, поэтому здесь только загрузка значений.
            audioManager.SetVolume(ParamUi,    PlayerPrefs.GetFloat(PrefUiVolume,    DefaultVolume));
            audioManager.SetVolume(ParamSfx,   PlayerPrefs.GetFloat(PrefSfxVolume,   DefaultVolume));
            audioManager.SetVolume(ParamMusic, PlayerPrefs.GetFloat(PrefMusicVolume, DefaultVolume));
        }
    }
}

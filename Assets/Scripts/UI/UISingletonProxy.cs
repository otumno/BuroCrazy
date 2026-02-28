// Assets/Scripts/UI/UISingletonProxy.cs
using UnityEngine;
using Managers;


namespace UI
{
    /// <summary>
    /// Скрипт-посредник. Позволяет вызывать методы Синглтонов через UnityEvent в Инспекторе, 
    /// даже если самого Синглтона нет на сцене в момент редактирования.
    /// </summary>
    public class UISingletonProxy : MonoBehaviour
    {
        // === Музыка (Music Player) ===

        public void Music_PlayArchiveTheme()
        {
            if (MusicPlayer.Instance != null) MusicPlayer.Instance.OpenArchiveMusic();
        }


        public void Music_CloseArchiveTheme()
        {
            if (MusicPlayer.Instance != null) MusicPlayer.Instance.CloseArchiveMusic();
        }


        public void Music_PlayOfficeTheme()
        {
            if (MusicPlayer.Instance != null) MusicPlayer.Instance.PlayDirectorsOfficeTheme();
        }


        // === Звуки (Audio Manager) ===

        public void Audio_PlayClickSound()
        {
            // Пример: если захотите звук клика на кнопку закрытия
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.UI_Click_Default);
        }


        // === Пауза (Main UI Manager) ===

        public void UI_PushPause()
        {
            if (MainUIManager.Instance != null) MainUIManager.Instance.PushPause();
        }


        public void UI_PopPause()
        {
            if (MainUIManager.Instance != null) MainUIManager.Instance.PopPause();
        }
    }
}

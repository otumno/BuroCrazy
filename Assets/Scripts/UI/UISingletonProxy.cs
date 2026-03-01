using UnityEngine;
using System.Collections;
using Managers;


namespace UI
{
    public class UISingletonProxy : MonoBehaviour
    {
        // === Музыка (Music Player) ===

        // Вызываем из OnOpen аниматора
        public void Music_PlayArchiveTheme()
        {
            StartCoroutine(DelayedPlayArchive());
        }


        private IEnumerator DelayedPlayArchive()
        {
            // Ждем 0.15 секунд реального времени, чтобы DOTween успел плавно начать анимацию окна
            yield return new WaitForSecondsRealtime(0.15f);
            
            if (MusicPlayer.Instance != null) 
            {
                MusicPlayer.Instance.OpenArchiveMusic();
            }
        }


        // Закрытие можно не задерживать, так как окно просто растворяется
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

// Файл: CameraAudioLink.cs
using UnityEngine;

public class CameraAudioLink : MonoBehaviour
{
    // Этот публичный метод будет вызываться из CameraToggle
    public void ToggleMuffledAudio(bool isMuffled)
    {
        // Находим живой экземпляр MusicPlayer и передаем ему команду
        if (MusicPlayer.Instance != null)
        {
            MusicPlayer.Instance.SetMuffled(isMuffled);
        }
    }
}
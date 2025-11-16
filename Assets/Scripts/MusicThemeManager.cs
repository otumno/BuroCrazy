using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicThemeManager : MonoBehaviour
{
    [Header("Музыкальные темы")]
    public AudioClip menuMusic;
    public AudioClip startOfDayMusic;
    public AudioClip gameplayMusic; // Можно взять из вашего старого MusicPlayer

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true; // Вся музыка будет зациклена
    }

    public void PlayMenuMusic()
    {
        PlayTrack(menuMusic);
    }

    public void PlayStartOfDayMusic()
    {
        PlayTrack(startOfDayMusic);
    }

    public void PlayGameplayMusic()
    {
        PlayTrack(gameplayMusic);
    }

    private void PlayTrack(AudioClip clip)
    {
        if (clip != null && audioSource.clip != clip)
        {
            audioSource.clip = clip;
            audioSource.Play();
        }
    }
}
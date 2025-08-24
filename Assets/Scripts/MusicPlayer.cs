using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    [Header("Плейлисты")]
    [Tooltip("Музыка, которая играет в течение дня")]
    public AudioClip[] dayTracks;
    [Tooltip("Один трек, который будет играть всю ночь (в цикле)")]
    public AudioClip nightTrack;

    private AudioSource audioSource;
    private int lastTrackIndex = -1;
    private bool isNightMusic = false;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        string period = ClientSpawner.CurrentPeriodName?.ToLower().Trim();

        // Логика для ночи
        if (period == "ночь")
        {
            if (!isNightMusic)
            {
                isNightMusic = true;
                audioSource.Stop();
                audioSource.clip = nightTrack;
                audioSource.loop = true;
                if(nightTrack != null) audioSource.Play();
            }
        }
        // Логика для дня
        else
        {
            if (isNightMusic)
            {
                isNightMusic = false;
                audioSource.Stop();
                audioSource.loop = false;
            }

            if (!audioSource.isPlaying)
            {
                PlayRandomDayTrack();
            }
        }
    }

    void PlayRandomDayTrack()
    {
        if (dayTracks.Length == 0) return;
        if (dayTracks.Length == 1)
        {
            if (audioSource.clip != dayTracks[0])
            {
                audioSource.clip = dayTracks[0];
                audioSource.Play();
            }
            audioSource.loop = true;
            return;
        }

        int newIndex;
        do { newIndex = Random.Range(0, dayTracks.Length); } while (newIndex == lastTrackIndex);
        
        lastTrackIndex = newIndex;
        audioSource.clip = dayTracks[lastTrackIndex];
        audioSource.Play();
    }
}
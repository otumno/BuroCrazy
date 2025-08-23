using UnityEngine;

// Требуем, чтобы на объекте со скриптом обязательно был компонент AudioSource
[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    // Массив, в который вы перетащите все ваши музыкальные треки в инспекторе
    public AudioClip[] musicTracks;

    private AudioSource audioSource;
    private int lastTrackIndex = -1; // Индекс последнего проигранного трека

    void Awake()
    {
        // Получаем доступ к компоненту AudioSource
        audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        // Выключаем зацикливание для отдельных треков,
        // так как мы будем управлять этим сами
        audioSource.loop = false;
    }

    void Update()
    {
        // Проверяем каждый кадр: если музыка сейчас не играет,
        // значит, предыдущий трек закончился или это первый запуск
        if (!audioSource.isPlaying)
        {
            PlayRandomTrack();
        }
    }

    void PlayRandomTrack()
    {
        // Если в массиве нет треков, ничего не делаем
        if (musicTracks.Length == 0)
        {
            Debug.LogWarning("В MusicPlayer не добавлено ни одного трека.");
            return;
        }

        // Если в плейлисте всего один трек, просто проигрываем его
        if (musicTracks.Length == 1)
        {
            // Проверяем, не назначен ли он уже, чтобы не вызывать Play() лишний раз
            if (audioSource.clip != musicTracks[0])
            {
                audioSource.clip = musicTracks[0];
                audioSource.Play();
            }
            // Включаем зацикливание, если трек один
            audioSource.loop = true;
            return;
        }

        // Выбираем новый случайный индекс до тех пор,
        // пока он не будет отличаться от индекса последнего трека
        int newIndex;
        do
        {
            newIndex = Random.Range(0, musicTracks.Length);
        } 
        while (newIndex == lastTrackIndex);

        // Сохраняем новый индекс как "последний"
        lastTrackIndex = newIndex;

        // Назначаем выбранный трек и проигрываем его
        audioSource.clip = musicTracks[lastTrackIndex];
        audioSource.Play();
    }
}
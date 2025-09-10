// Файл: MusicPlayer.cs
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement; // Необходимо для работы со сценами

[RequireComponent(typeof(AudioSource))] //
public class MusicPlayer : MonoBehaviour
{
    // --- НОВЫЙ КОД: Синглтон и "Бессмертие" ---
    public static MusicPlayer Instance { get; private set; }

    // Метод Awake вызывается самым первым при запуске скрипта
    void Awake()
    {
        // Проверяем, не существует ли уже другой экземпляр MusicPlayer
        if (Instance != null && Instance != this)
        {
            // Если да, то этот - дубликат, и его нужно уничтожить
            Destroy(gameObject);
            return;
        }
        // Если нет, то этот экземпляр становится основным
        Instance = this;
        // Говорим Unity не уничтожать этот объект при загрузке новой сцены
        //DontDestroyOnLoad(gameObject);

        // Код из старого Awake
        audioSource = GetComponent<AudioSource>(); //
        lowPassFilter = GetComponent<AudioLowPassFilter>(); //
        initialVolume = audioSource.volume; //
    }
    // --- КОНЕЦ НОВОГО КОДА ---

    [Header("Музыкальные темы")]
    public AudioClip menuTheme; //
    public AudioClip directorsOfficeTheme; //
    [Header("Игровые плейлисты")]
    public AudioClip[] dayTracks; //
    public AudioClip nightTrack; //
    public AudioClip radioSwitchSound; //
    [Header("Эффект приглушения")]
    public float muffledFrequency = 1200f; //
    [Range(0f, 1f)]
    public float muffledVolume = 0.5f; //
    public float fadeDuration = 0.5f; //

    private AudioSource audioSource;
    private AudioLowPassFilter lowPassFilter;
    private Coroutine effectsCoroutine;
    private int lastTrackIndex = -1; //
    private bool isNightMusic = false; //
    private float initialVolume;

    // --- НОВЫЙ КОД: Логика, основанная на сценах ---
    void Start()
    {
        // Подписываемся на событие, которое срабатывает каждый раз, когда загружается новая сцена
        SceneManager.sceneLoaded += OnSceneLoaded;
        // При самом первом запуске игры, мы в главном меню, поэтому включаем соответствующую тему
        PlayMenuTheme();
    }

    // Этот метод теперь вызывается автоматически при смене сцены
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Проверяем имя загруженной сцены
        if (scene.name == "MainMenuScene") // <-- Укажите здесь ТОЧНОЕ имя вашей сцены с меню
        {
            PlayMenuTheme();
        }
        else if (scene.name == "GameScene") // <-- Укажите здесь ТОЧНОЕ имя вашей игровой сцены
        {
            PlayGameplayMusic();
        }
    }

    // Старый метод Update() больше не нужен, так как логика переключения треков (день/ночь)
    // должна управляться из игрового менеджера (например, ClientSpawner) внутри игровой сцены.
    // void Update() { ... }

    // --- КОНЕЦ НОВОГО КОДА ---
    
    public void RequestNextTrack()
    {
        if (isNightMusic || Time.timeScale == 0f || dayTracks.Length == 0) //
        {
            return; //
        }

        if (radioSwitchSound != null) //
        {
            if (Camera.main != null) //
            {
                AudioSource.PlayClipAtPoint(radioSwitchSound, Camera.main.transform.position); //
            }
        }
        
        PlayRandomDayTrack(); //
    }
    
    private void PlayTrack(AudioClip clip, bool loop)
    {
        if (clip == null) return; //
        if (audioSource.clip == clip && audioSource.isPlaying) return; //
        
        audioSource.clip = clip; //
        audioSource.loop = loop; //
        audioSource.Play(); //
    }
    
    void PlayRandomDayTrack()
    {
        if (dayTracks.Length == 0) return; //
        if (dayTracks.Length == 1) //
        {
            PlayTrack(dayTracks[0], true); //
            return; //
        }

        int newIndex;
        do { newIndex = Random.Range(0, dayTracks.Length); //
        } while (newIndex == lastTrackIndex);
        
        lastTrackIndex = newIndex; //
        PlayTrack(dayTracks[lastTrackIndex], false); //
    }
    
    public void PlayMenuTheme() { PlayTrack(menuTheme, true); } //
    public void PlayDirectorsOfficeTheme() { PlayTrack(directorsOfficeTheme, true); }
    public void PlayGameplayMusic() { audioSource.Stop(); //
        isNightMusic = false; } //
    public void SetMuffled(bool isMuffled) { if (effectsCoroutine != null) StopCoroutine(effectsCoroutine); //
        if (isNightMusic) { if (lowPassFilter != null) lowPassFilter.cutoffFrequency = 22000f; audioSource.volume = initialVolume; return; } float targetVolume = isMuffled ? //
        muffledVolume : initialVolume; //
 float targetFrequency = isMuffled ? muffledFrequency : 22000f; effectsCoroutine = StartCoroutine(LerpAudioEffects(targetVolume, targetFrequency)); //
    }
    private IEnumerator LerpAudioEffects(float targetVol, float targetFreq) { float startVol = audioSource.volume; //
        float startFreq = lowPassFilter != null ? lowPassFilter.cutoffFrequency : 22000f; float time = 0; //
        while (time < fadeDuration) { time += Time.unscaledDeltaTime; float progress = time / fadeDuration; audioSource.volume = Mathf.Lerp(startVol, targetVol, progress); //
            if (lowPassFilter != null) { lowPassFilter.cutoffFrequency = Mathf.Lerp(startFreq, targetFreq, progress); } yield return null; } audioSource.volume = targetVol; //
        if (lowPassFilter != null) { lowPassFilter.cutoffFrequency = targetFreq; } effectsCoroutine = null; yield break; } //
}
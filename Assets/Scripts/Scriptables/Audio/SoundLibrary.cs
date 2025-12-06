using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Audio; // Нужен для микшера

// 1. Список всех звуков в игре (дополняй по мере необходимости)
public enum SoundID
{
    None,
    
    // UI
    UI_Click_Default,
    UI_Hover,
    UI_Popup_Open,
    UI_Money_Income,
    UI_Stamp_Approve,
    UI_Stamp_Reject,
    
    // Music (Теги для треков)
    Music_Menu,
    Music_Gameplay_Day,
    Music_Gameplay_Night,
    
    // World / Characters
    Door_Open,
    Door_Close,
    Footstep_Carpet,
    Footstep_Tile,
    Client_Angry,
    Client_Happy,
    
    // Special
    Time_Period_Change
}

// 2. Настройки одного звука
[System.Serializable]
public class SoundData
{
    public SoundID id;
    public AudioClip clip;
    
    [Header("Settings")]
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitch = 1f;
    [Tooltip("Случайное отклонение питча (для вариативности)")]
    [Range(0f, 0.5f)] public float pitchVariance = 0.1f;
    
    [Header("Routing")]
    [Tooltip("В какую группу микшера отправить? (Music, SFX, UI)")]
    public AudioMixerGroup mixerGroup;
}

// 3. База данных (ScriptableObject)
[CreateAssetMenu(fileName = "AudioLibrary", menuName = "Bureau/Audio Library")]
public class AudioLibrary : ScriptableObject
{
    public List<SoundData> sounds;

    // Словарь для быстрого поиска (инициализируется при старте игры)
    private Dictionary<SoundID, SoundData> soundDictionary;

    public void Initialize()
    {
        soundDictionary = new Dictionary<SoundID, SoundData>();
        foreach (var sound in sounds)
        {
            if (sound.id != SoundID.None && !soundDictionary.ContainsKey(sound.id))
            {
                soundDictionary.Add(sound.id, sound);
            }
        }
    }

    public SoundData GetSound(SoundID id)
    {
        if (soundDictionary == null) Initialize();
        
        if (soundDictionary.TryGetValue(id, out SoundData data))
        {
            return data;
        }
        return null;
    }
}
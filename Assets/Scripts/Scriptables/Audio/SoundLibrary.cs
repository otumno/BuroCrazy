using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Audio;
using UnityEngine.Serialization; // Нужен для микшера

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
    
    // можно так-то в массив переделать, чтобы рандомно 1 из проигрывать
    public AudioClip clip;
    
    [Header("Settings")]
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitch = 1f;
    [Range(0f, 0.5f)] public float pitchDelta = 0.1f;
    
    [Header("2D / 3D")]
    [Range(0f, 1f)] public float  SpatialBlend = 1.0f;
    
    [Header("3D")]
    public AudioRolloffMode Rolloff  = AudioRolloffMode.Linear;
    [Range(0f, 5f)] public float  DopplerLevel = 1;
    public float DistanceMin  = 0f;
    public float DistanceMax  = 50.0f;
    
    [Header("Routing")]
    [Tooltip("В какую группу микшера отправить? (Music, SFX, UI)")]
    public AudioMixerGroup mixerGroup;
}

// 3. База данных (ScriptableObject)
[CreateAssetMenu(fileName = "AudioLibrary", menuName = "Bureau/Audio Library")]
public class AudioLibrary : ScriptableObject
{
    [JsonProperty]
    public List<SoundData> sounds;

    [JsonIgnore]
    private Dictionary<SoundID, SoundData> _dataOverId = new();

    private void Initialize()
    {
        _dataOverId = new Dictionary<SoundID, SoundData>();
        for (int k = 0; k < sounds.Count; k++)
        {
            var sound = sounds[k];
            
            // пишем ошибку, что звук есть в списке, но не настроен
            if (sound.id == SoundID.None)
            {
                Debug.LogError($"SoundData {k} SoundID == None");
                continue;
            }

            // добавляем и пишем ошибку, если дубликат
            if (!_dataOverId.TryAdd(sound.id, sound))
                Debug.LogError($"SoundData {k}. {sound.id} already added!");
        }
    }

    public SoundData GetSound(SoundID id)
    {
        if (_dataOverId.Count == 0)
            Initialize();
        
        if (_dataOverId.TryGetValue(id, out var data))
            return data;
        
        // пишем ошибку, если звук не найден
        Debug.LogError($"SoundData {id} not found in audio library!");
        return null;
    }
}
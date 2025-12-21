using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Scriptables.Audio
{
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "Bureau/Audio Library")]
    public class SoundLibrary : ScriptableObject
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
                if (!sound.Validate(k))
                    continue;

                // не добавляем и пишем ошибку, если дубликат
                if (!_dataOverId.TryAdd(sound.id, sound))
                    Debug.LogError($"SoundData {k}. {sound.id} already added!");
            }
        }

        public SoundData GetSound(SoundID id)
        {
            if (_dataOverId.Count == 0)
                Initialize();
        
            return _dataOverId.TryGetValue(id, out var data) ? data : null;
        }
    }
}
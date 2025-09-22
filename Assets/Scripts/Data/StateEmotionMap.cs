// Файл: StateEmotionMap.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "StateEmotionMap", menuName = "My Game/State to Emotion Map")]
public class StateEmotionMap : ScriptableObject
{
    // --- НОВОЕ: Добавляем тип персонажа, для которого эта карта предназначена ---
    [Tooltip("Выберите тип персонажа, для которого настраивается эта карта эмоций")]
    public CharacterType targetCharacterType;

    [System.Serializable]
    public class StateEmotionPair
    {
        [Tooltip("Точное название состояния из enum (например, 'Working', 'Patrolling', 'OnBreak')")]
        public string stateName;
        [Tooltip("Какую эмоцию показывать для этого состояния")]
        public Emotion emotion;
    }

    [Tooltip("Список сопоставлений 'Состояние -> Эмоция' для одного типа персонажей")]
    public List<StateEmotionPair> mappings;

    public bool TryGetEmotionForState(string stateName, out Emotion foundEmotion)
    {
        var mapping = mappings.FirstOrDefault(m => m.stateName == stateName);
        if (mapping != null)
        {
            foundEmotion = mapping.emotion;
            return true;
        }

        foundEmotion = Emotion.Neutral;
        return false;
    }
}
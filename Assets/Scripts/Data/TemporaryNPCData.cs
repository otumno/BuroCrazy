using UnityEngine;
using Enums;
using Characters;
using Scriptables.Audio;

namespace Data
{
    [CreateAssetMenu(fileName = "TempNPC_New", menuName = "Bureau/Temporary NPC Data")]
    public class TemporaryNPCData : ScriptableObject
    {
        [Header("Визуал")]
        public Gender gender;
        public EmotionSpriteCollection spriteCollection;
        public StateEmotionMap stateEmotionMap;
        
        [Header("Анимация ходьбы")]
        public float animationSpeed = 0.3f;
        
        [Header("Аксессуары")]
        public GameObject accessoryPrefab; // швабра, клоунский нос и т.д.
        
        [Header("Голос")]
        public VoiceData voiceProfile;
    }
}

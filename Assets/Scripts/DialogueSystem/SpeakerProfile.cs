using UnityEngine;
using Scriptables.Audio;

namespace DialogueSystem.Data
{
    [CreateAssetMenu(fileName = "NewSpeaker", menuName = "Bureau/Dialogue/Speaker Profile")]
    public class SpeakerProfile : ScriptableObject
    {
        [Tooltip("ID, который мы пишем в ноде (например: Mascot, Secretary)")]
        public string speakerID; 
        
        [Tooltip("Имя, которое увидит игрок")]
        public string displayName;

        [Tooltip("Портрет для диалога")]
        public Sprite portrait;

        [Tooltip("Голос персонажа")]
        public VoiceData voice;
    }
}
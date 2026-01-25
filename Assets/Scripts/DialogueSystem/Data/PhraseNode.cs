// Assets/Scripts/DialogueSystem/Data/PhraseNode.cs
using UnityEngine;

namespace DialogueSystem.Data
{
    [CreateAssetMenu(menuName = "Bureau/Dialogue/Nodes/Phrase Node")]
    public class PhraseNode : DialogueNode
    {
        // ... (предыдущие поля: speakerID, text) ...
        public string speakerID;
        [TextArea(3, 5)] public string text;
        public Sprite speakerPortrait;
        public AudioClip voiceClip; 

        // НОВОЕ ПОЛЕ
        public AudioClip appearSound; // Звук при появлении фразы (например "Вжик" или "Тук")

        [Header("Изображение для ноды")]
        [Tooltip("Изображение, которое будет показано в этой ноде (необязательно)")]
        public Sprite nodeImage;

        [Header("Связь")]
        public DialogueNode nextNode;

        public override NodeType GetNodeType() => NodeType.Phrase;
    }
}
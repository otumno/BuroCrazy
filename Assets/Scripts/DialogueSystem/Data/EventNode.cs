// Файл: Assets/Scripts/DialogueSystem/Data/EventNode.cs
using UnityEngine;

namespace DialogueSystem.Data
{
    [CreateAssetMenu(menuName = "Bureau/Dialogue/Nodes/Event Node")]
    public class EventNode : DialogueNode
    {
        public enum EventType { SetFlag, AddMoney, AddStrike, EndDialogue }
        
        public EventType eventType;
        
        [Header("Логика")]
        [Tooltip("Имя флага для SetFlag")]
        public string flagKey;
        [Tooltip("Значение для флага или сумма денег")]
        public int intValue;

        [Header("Обратная связь (Визуал/Звук)")]
        [Tooltip("Если заполнить, диалог покажет это сообщение и будет ждать клика.")]
        [TextArea(2,3)] public string notificationText;
        [Tooltip("Звук события (звон монет, удар и т.д.)")]
        public AudioClip soundEffect;

        [Header("Изображение для ноды")]
        [Tooltip("Изображение, которое будет показано в этой ноде (необязательно)")]
        public Sprite nodeImage;

        [Header("Связь")]
        public DialogueNode nextNode; // Авто-переход

        public override NodeType GetNodeType() => NodeType.Event;
    }
}
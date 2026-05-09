using UnityEngine;

namespace DialogueSystem.Data
{
    public enum DialogueType { World, Phone, None }
    
    [CreateAssetMenu(menuName = "Bureau/Dialogue/Nodes/Start Node")]
    public class StartNode : DialogueNode
    {
        [Header("Настройки старта")]
        public AudioClip startSoundOverride; // Кастомный звук начала
        
        [Header("Тип диалога")]
        public DialogueType dialogueType = DialogueType.World;
        
        [Header("Фон диалога")]
        [Tooltip("Фон, который будет использоваться по умолчанию для всех нод. Можно переопределить в конкретной ноде.")]
        public Sprite defaultBackground;

        [Header("Связь")]
        public DialogueNode nextNode;

        public override NodeType GetNodeType() => NodeType.Start;
    }
}
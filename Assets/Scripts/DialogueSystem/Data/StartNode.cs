using UnityEngine;

namespace DialogueSystem.Data
{
    [CreateAssetMenu(menuName = "Bureau/Dialogue/Nodes/Start Node")]
    public class StartNode : DialogueNode
    {
        [Header("Настройки старта")]
        public AudioClip startSoundOverride; // Кастомный звук начала
        
        [Header("Связь")]
        public DialogueNode nextNode;

        public override NodeType GetNodeType() => NodeType.Start;
    }
}
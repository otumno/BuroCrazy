using UnityEngine;

namespace DialogueSystem.Data
{
    [CreateAssetMenu(menuName = "Bureau/Dialogue/Nodes/End Node")]
    public class EndNode : DialogueNode
    {
        [Header("Звук завершения")]
        public AudioClip endSound; // Звук (например, повесить трубку)

        public override NodeType GetNodeType() => NodeType.End;
    }
}
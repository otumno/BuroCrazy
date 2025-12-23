using UnityEngine;

namespace DialogueSystem.Data
{
    [CreateAssetMenu(menuName = "Bureau/Dialogue/Nodes/End Node")]
    public class EndNode : DialogueNode
    {
        public override NodeType GetNodeType() => NodeType.End; // Добавь End в enum в DialogueNode.cs!
    }
}
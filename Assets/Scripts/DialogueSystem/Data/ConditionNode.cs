// Assets/Scripts/DialogueSystem/Data/ConditionNode.cs
using UnityEngine;

namespace DialogueSystem.Data
{
    [CreateAssetMenu(menuName = "Bureau/Dialogue/Nodes/Condition Node")]
    public class ConditionNode : DialogueNode
    {
        [Header("Логика")]
        public string conditionKey; // MONEY, CORRUPTION или свой флаг
        public string operation;    // >, <, ==, >=, <=
        public int conditionValue;

        [Header("Ветвление")]
        public DialogueNode trueNode;  // Куда идти, если Истина
        public DialogueNode falseNode; // Куда идти, если Ложь

        public override NodeType GetNodeType() => NodeType.Condition;
    }
}
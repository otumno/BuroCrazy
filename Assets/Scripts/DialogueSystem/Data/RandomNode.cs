using UnityEngine;
using System.Collections.Generic;

namespace DialogueSystem.Data
{
    [CreateAssetMenu(menuName = "Bureau/Dialogue/Nodes/Random Node")]
    public class RandomNode : DialogueNode
    {
        [System.Serializable]
        public class RandomOutcome
        {
            public DialogueNode nextNode;
            [Range(0f, 1f)] public float chance = 1f; // Вес вероятности
        }

        [TextArea(2, 3)] public string developerComment; // Для удобства в редакторе
        public List<RandomOutcome> outcomes = new List<RandomOutcome>();

        public override NodeType GetNodeType() => NodeType.Choice; // Используем Choice как базу для отрисовки или добавь Random в enum
    }
}
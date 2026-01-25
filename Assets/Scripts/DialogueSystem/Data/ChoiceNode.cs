// Файл: Assets/Scripts/DialogueSystem/Data/ChoiceNode.cs
using UnityEngine;
using System.Collections.Generic;

namespace DialogueSystem.Data
{
    [CreateAssetMenu(menuName = "Bureau/Dialogue/Nodes/Choice Node")]
    public class ChoiceNode : DialogueNode
    {
        [System.Serializable]
        public class ChoiceOption
        {
            public string text;
            public DialogueNode nextNode;
            
            [Header("Условия (Опционально)")]
            [Tooltip("Ключ флага, например 'Met_Inspector'")]
            public string conditionKey; 
            [Tooltip("Тип проверки: > < == !=")]
            public string operation; 
            public int conditionValue;
        }

        [TextArea(2, 3)] public string queryText; // Вопрос игроку (например "Что ответить?")
        public List<ChoiceOption> options = new List<ChoiceOption>();

        [Header("Изображение для ноды")]
        [Tooltip("Изображение, которое будет показано в этой ноде (необязательно)")]
        public Sprite nodeImage;

        public override NodeType GetNodeType() => NodeType.Choice;
    }
}
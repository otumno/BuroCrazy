// Assets/Scripts/DialogueSystem/Data/DialogueNode.cs
using UnityEngine;
using System.Collections.Generic;

namespace DialogueSystem.Data
{
    // Добавляем 'Condition' в конец списка
    public enum NodeType { Start, Phrase, Choice, Event, Random, End, Condition }

    public abstract class DialogueNode : ScriptableObject
    {
        [HideInInspector] public string id;

        /// <summary>
        /// Человеко-читаемый стабильный идентификатор ноды (например, "node_1", "choice_main").
        /// Используется при импорте/экспорте диалогов из JSON (DeepSeek / генератор контента).
        /// Если не задан — нода считается «локальной» и не участвует в обратной ссылке.
        /// </summary>
        public string nodeID;

        [HideInInspector] public Rect graphPosition; // Позиция в визуальном редакторе
        public abstract NodeType GetNodeType();
    }
}
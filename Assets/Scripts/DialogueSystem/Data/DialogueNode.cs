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
        [HideInInspector] public Rect graphPosition; // Позиция в визуальном редакторе
        public abstract NodeType GetNodeType();
    }
}
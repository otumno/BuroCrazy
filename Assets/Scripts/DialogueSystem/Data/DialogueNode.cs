// Файл: Assets/Scripts/DialogueSystem/Data/DialogueNode.cs
using UnityEngine;
using System.Collections.Generic;

namespace DialogueSystem.Data
{
    public enum NodeType { Start, Phrase, Choice, Event, Random, End }

    public abstract class DialogueNode : ScriptableObject
    {
        [HideInInspector] public string id;
        [HideInInspector] public Rect graphPosition; // Позиция в визуальном редакторе
        public abstract NodeType GetNodeType();
    }
}
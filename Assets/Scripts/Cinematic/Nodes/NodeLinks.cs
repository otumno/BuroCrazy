// Файл: Assets/Scripts/Cinematic/Nodes/NodeLinks.cs
// Сериализуемые связи между узлами
// Используется для визуального редактора

using System;
using UnityEngine;

namespace BuroCrazy.CinematicSystem
{
    /// <summary>
    /// Связь между узлами с дополнительными данными
    /// </summary>
    [Serializable]
    public class NodeLink
    {
        /// <summary>
        /// ID исходного узла
        /// </summary>
        public string fromNodeId;
        
        /// <summary>
        /// ID целевого узла
        /// </summary>
        public string toNodeId;
        
        /// <summary>
        /// Тип связи (default, true, false, etc.)
        /// </summary>
        public string linkType = "default";
        
        /// <summary>
        /// Цвет связи
        /// </summary>
        public Color linkColor = Color.white;
        
        public NodeLink() { }
        
        public NodeLink(string from, string to)
        {
            fromNodeId = from;
            toNodeId = to;
        }
    }
    
    /// <summary>
    /// Позиция узла в редакторе (для сериализации)
    /// </summary>
    [Serializable]
    public class NodePosition
    {
        public string nodeId;
        public float x;
        public float y;
        public float width;
        public float height;
        
        public Rect ToRect()
        {
            return new Rect(x, y, width, height);
        }
        
        public static NodePosition FromRect(string id, Rect rect)
        {
            return new NodePosition
            {
                nodeId = id,
                x = rect.x,
                y = rect.y,
                width = rect.width,
                height = rect.height
            };
        }
    }
}
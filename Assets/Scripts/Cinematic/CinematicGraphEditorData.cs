// Файл: Assets/Scripts/Cinematic/CinematicGraphEditorData.cs
// Вспомогательные данные для редактора графа

using System.Collections.Generic;
using UnityEngine;

namespace BuroCrazy.CinematicSystem
{
    /// <summary>
    /// Данные для визуального редактора графа.
    /// Хранит информацию о позициях узлов и связях.
    /// </summary>
    [System.Serializable]
    public class CinematicGraphEditorData
    {
        /// <summary>
        /// ID графа
        /// </summary>
        public string graphID;
        
        /// <summary>
        /// Позиции узлов
        /// </summary>
        public List<NodePosition> nodePositions = new List<NodePosition>();
        
        /// <summary>
        /// Связи между узлами
        /// </summary>
        public List<NodeLink> links = new List<NodeLink>();
        
        /// <summary>
        /// Масштаб редактора
        /// </summary>
        public float zoomLevel = 1f;
        
        /// <summary>
        /// Позиция прокрутки редактора
        /// </summary>
        public Vector2 scrollPosition;
        
        /// <summary>
        /// Получить позицию узла
        /// </summary>
        public Rect? GetNodePosition(string nodeId)
        {
            var pos = nodePositions.Find(p => p.nodeId == nodeId);
            return pos?.ToRect();
        }
        
        /// <summary>
        /// Установить позицию узла
        /// </summary>
        public void SetNodePosition(string nodeId, Rect rect)
        {
            var existing = nodePositions.Find(p => p.nodeId == nodeId);
            if (existing != null)
            {
                existing.x = rect.x;
                existing.y = rect.y;
                existing.width = rect.width;
                existing.height = rect.height;
            }
            else
            {
                nodePositions.Add(NodePosition.FromRect(nodeId, rect));
            }
        }
        
        /// <summary>
        /// Добавить связь
        /// </summary>
        public void AddLink(string fromId, string toId, string type = "default")
        {
            var existing = links.Find(l => l.fromNodeId == fromId && l.linkType == type);
            if (existing != null)
            {
                existing.toNodeId = toId;
            }
            else
            {
                links.Add(new NodeLink(fromId, toId) { linkType = type });
            }
        }
        
        /// <summary>
        /// Получить связи для узла
        /// </summary>
        public List<NodeLink> GetLinksFrom(string nodeId)
        {
            return links.FindAll(l => l.fromNodeId == nodeId);
        }
    }
}
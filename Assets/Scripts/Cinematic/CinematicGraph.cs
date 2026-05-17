// === FILE: Assets/Scripts/Cinematic/CinematicGraph.cs ===
using System.Collections.Generic;
using UnityEngine;
using CinematicSystem.Nodes;

namespace CinematicSystem
{
    /// <summary>
    /// ScriptableObject, представляющий кинематический граф - последовательность узлов для постановочных сцен.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCinematicGraph", menuName = "Bureau/Cinematic/Cinematic Graph")]
    public class CinematicGraph : ScriptableObject
    {
        [Tooltip("Уникальный ID графа")]
        public string graphID;
        
        [Tooltip("Название для отображения")]
        public string graphName;
        
        [Tooltip("Список всех узлов графа")]
        public List<CinematicNode> allNodes = new List<CinematicNode>();
        
        [Tooltip("Стартовый узел")]
        public CinematicNode startNode;
        
        [Tooltip("Является ли этот граф фоновым (выполняется без блокировки управления)")]
        public bool isBackground = false;
        
        /// <summary>
        /// Параметры времени выполнения, передаваемые в граф при запуске.
        /// Используется для VIP-сценариев и динамических данных.
        /// </summary>
        [System.NonSerialized]
        public Dictionary<string, object> runtimeParameters = new Dictionary<string, object>();

        /// <summary>
        /// Создаёт новый узел указанного типа, добавляет его в список и возвращает.
        /// </summary>
        public T CreateNode<T>() where T : CinematicNode
        {
            T node = ScriptableObject.CreateInstance<T>();
            node.id = System.Guid.NewGuid().ToString();
            node.name = $"{typeof(T).Name}_{allNodes.Count}";
            allNodes.Add(node);
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.AddObjectToAsset(node, this);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
            return node;
        }

        /// <summary>
        /// Создаёт новый узел указанного типа (по System.Type), добавляет его в список и возвращает.
        /// </summary>
        public CinematicNode CreateNode(System.Type nodeType)
        {
            if (!typeof(CinematicNode).IsAssignableFrom(nodeType))
            {
                Debug.LogError($"[CinematicGraph] Type {nodeType.Name} does not derive from CinematicNode");
                return null;
            }

            CinematicNode node = ScriptableObject.CreateInstance(nodeType) as CinematicNode;
            if (node == null)
            {
                Debug.LogError($"[CinematicGraph] Failed to create instance of {nodeType.Name}");
                return null;
            }

            node.id = System.Guid.NewGuid().ToString();
            node.name = $"{nodeType.Name}_{allNodes.Count}";
            allNodes.Add(node);
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.AddObjectToAsset(node, this);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
            return node;
        }

        /// <summary>
        /// Удаляет узел и все ссылки на него.
        /// </summary>
        public void DeleteNode(CinematicNode node)
        {
            if (!allNodes.Contains(node)) return;

            // Удаляем ссылки из других узлов
            foreach (var n in allNodes)
            {
                if (n is ConditionNode cond)
                {
                    if (cond.trueNode == node) cond.trueNode = null;
                    if (cond.falseNode == node) cond.falseNode = null;
                }
                else if (n is NextNode next)
                {
                    if (next.nextNode == node) next.nextNode = null;
                }
            }

            allNodes.Remove(node);
            if (startNode == node) startNode = null;
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.RemoveObjectFromAsset(node);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
        }
        
        /// <summary>
        /// Валидация графа.
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            if (startNode == null)
            {
                errorMessage = "Start node не установлен";
                return false;
            }
            
            if (allNodes.Count == 0)
            {
                errorMessage = "Граф не содержит узлов";
                return false;
            }
            
            if (!allNodes.Contains(startNode))
            {
                errorMessage = "Start node не содержится в списке узлов";
                return false;
            }
            
            errorMessage = null;
            return true;
        }
    }
}
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
    public class CinematicGraph : ScriptableObject, ISerializationCallbackReceiver
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
        /// Сериализованная связь между узлами.
        /// </summary>
        [System.Serializable]
        public class NodeLink
        {
            public string fromNodeId;
            public string toNodeId;
            public string linkType;
        }
        
        [SerializeField] private List<NodeLink> links = new List<NodeLink>();
        
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

        /// <summary>
        /// Перестраивает список связей (links) из текущих ссылок в узлах.
        /// Вызывается автоматически перед сериализацией.
        /// </summary>
        public void RebuildLinksFromNodes()
        {
            links.Clear();
            foreach (var node in allNodes)
            {
                if (node == null) continue;
                string fromId = node.id;
                
                if (node is Nodes.StartNode start && start.nextNode != null)
                    links.Add(new NodeLink { fromNodeId = fromId, toNodeId = start.nextNode.id, linkType = "next" });
                else if (node is NextNode next && next.nextNode != null)
                    links.Add(new NodeLink { fromNodeId = fromId, toNodeId = next.nextNode.id, linkType = "next" });
                else if (node is Nodes.ConditionNode cond)
                {
                    if (cond.trueNode != null) links.Add(new NodeLink { fromNodeId = fromId, toNodeId = cond.trueNode.id, linkType = "true" });
                    if (cond.falseNode != null) links.Add(new NodeLink { fromNodeId = fromId, toNodeId = cond.falseNode.id, linkType = "false" });
                }
                else if (node is Nodes.RandomNode rand && rand.outcomes != null)
                {
                    for (int i = 0; i < rand.outcomes.Count; i++)
                        if (rand.outcomes[i].nextNode != null)
                            links.Add(new NodeLink { fromNodeId = fromId, toNodeId = rand.outcomes[i].nextNode.id, linkType = i.ToString() });
                }
            }
        }

        /// <summary>
        /// Восстанавливает ссылки в узлах из списка связей (links).
        /// Вызывать после загрузки графа.
        /// </summary>
        public void ApplyLinks()
        {
            var nodeDict = new Dictionary<string, CinematicNode>();
            foreach (var n in allNodes) if (n != null) nodeDict[n.id] = n;
            
            foreach (var link in links)
            {
                if (!nodeDict.TryGetValue(link.fromNodeId, out var from)) continue;
                if (!nodeDict.TryGetValue(link.toNodeId, out var to)) continue;
                
                if (from is Nodes.StartNode start && link.linkType == "next") start.nextNode = to;
                else if (from is NextNode next && link.linkType == "next") next.nextNode = to;
                else if (from is Nodes.ConditionNode cond)
                {
                    if (link.linkType == "true") cond.trueNode = to;
                    else if (link.linkType == "false") cond.falseNode = to;
                }
                else if (from is Nodes.RandomNode rand && int.TryParse(link.linkType, out int idx))
                {
                    if (idx < rand.outcomes.Count) rand.outcomes[idx].nextNode = to;
                }
            }
        }

        /// <summary>
        /// Публичный метод для ручного восстановления ссылок (вызывать после загрузки графа).
        /// </summary>
        public void RestoreLinks()
        {
            ApplyLinks();
        }

        /// <summary>
        /// Вызывается Unity перед сериализацией объекта.
        /// Перестраиваем список связей из текущих ссылок в узлах.
        /// </summary>
        public void OnBeforeSerialize()
        {
            RebuildLinksFromNodes();
        }

        /// <summary>
        /// Вызывается Unity после десериализации объекта.
        /// Не вызываем ApplyLinks здесь, так как allNodes ещё не загружены.
        /// Вместо этого вызываем RestoreLinks() в PopulateView/Play.
        /// </summary>
        public void OnAfterDeserialize()
        {
            // Не восстанавливаем здесь - allNodes ещё не готовы
        }
    }
}
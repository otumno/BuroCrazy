// === FILE: Assets/Scripts/Cinematic/CinematicGraphLibrary.cs ===
// Runtime-библиотека для загрузки CinematicGraph по имени
// Загружает графы из Assets/Data/CinematicGraphs/

using UnityEngine;
using System.Collections.Generic;
using CinematicSystem;

namespace CinematicSystem
{
    /// <summary>
    /// Статическая библиотека для поиска CinematicGraph ресурсов.
    /// Загружает графы из Assets/Data/CinematicGraphs/ или Resources/CinematicGraphs/
    /// </summary>
    public static class CinematicGraphLibrary
    {
        private const string DATA_GRAPHS_PATH = "Assets/Data/CinematicGraphs/";
        
        // Кэш загруженных графов по имени
        private static readonly Dictionary<string, CinematicGraph> graphCache = new Dictionary<string, CinematicGraph>();

        /// <summary>
        /// Загрузить граф по имени. Сначала проверяет кэш, потом ищет в стандартных путях.
        /// </summary>
        public static CinematicGraph LoadGraph(string graphName)
        {
            if (string.IsNullOrEmpty(graphName))
            {
                Debug.LogError("[CinematicGraphLibrary] graphName is null or empty!");
                return null;
            }

            // Проверяем кэш
            if (graphCache.TryGetValue(graphName, out var cachedGraph))
            {
                return cachedGraph;
            }

            CinematicGraph graph = null;

            // 1. Пробуем загрузить из Resources/CinematicGraphs/
            graph = Resources.Load<CinematicGraph>($"CinematicGraphs/{graphName}");
            if (graph != null)
            {
                Debug.Log($"[CinematicGraphLibrary] Загружен граф из Resources/CinematicGraphs/{graphName}");
                graphCache[graphName] = graph;
                return graph;
            }

            // 2. Пробуем загрузить напрямую из Resources
            graph = Resources.Load<CinematicGraph>(graphName);
            if (graph != null)
            {
                Debug.Log($"[CinematicGraphLibrary] Загружен граф из Resources/{graphName}");
                graphCache[graphName] = graph;
                return graph;
            }

            // 3. В Editor/Runtime - загружаем напрямую из Assets/Data/CinematicGraphs/
            #if UNITY_EDITOR
            string assetPath = DATA_GRAPHS_PATH + graphName + ".asset";
            graph = UnityEditor.AssetDatabase.LoadAssetAtPath<CinematicGraph>(assetPath);
            if (graph != null)
            {
                Debug.Log($"[CinematicGraphLibrary] Загружен граф из Assets: {assetPath}");
                graphCache[graphName] = graph;
                
                // ВОССТАНАВЛИВАЕМ СВЯЗИ МЕЖДУ НОДАМИ
                RestoreNodeLinks(graph);
                
                return graph;
            }
            #endif

            // 4. Пробуем через Addressables если доступно
            #if UNITY_ADDRESSABLES_EXISTS
            var handle = Unity.AddressableAssets.Addressables.LoadAssetAsync<CinematicGraph>(graphName);
            if (handle.IsDone && handle.Result != null)
            {
                Debug.Log($"[CinematicGraphLibrary] Загружен граф через Addressables: {graphName}");
                graphCache[graphName] = handle.Result;
                return handle.Result;
            }
            #endif

            Debug.LogError($"[CinematicGraphLibrary] Граф не найден: {graphName}");
            return null;
        }

        /// <summary>
        /// Загрузить граф по graphID (ищет по всем ассетам в проекте - тяжёлая операция).
        /// </summary>
        public static CinematicGraph LoadGraphByID(string graphID)
        {
            if (string.IsNullOrEmpty(graphID))
                return null;

            #if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets($"t:CinematicGraph");
            foreach (var guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var graph = UnityEditor.AssetDatabase.LoadAssetAtPath<CinematicGraph>(path);
                if (graph != null && graph.graphID == graphID)
                {
                    return graph;
                }
            }
            #endif

            return null;
        }

        /// <summary>
        /// Восстанавливает связи между нодами на основе их ID.
        /// Это нужно потому что Unity не всегда корректно сериализует перекрёстные ссылки между sub-assets.
        /// </summary>
        private static void RestoreNodeLinks(CinematicGraph graph)
        {
            if (graph == null || graph.allNodes == null || graph.allNodes.Count == 0)
                return;
                
            Debug.Log($"[CinematicGraphLibrary] RestoreNodeLinks: восстанавливаем связи для {graph.allNodes.Count} нод");
            
            // Создаём карту ID -> Node
            var nodeMap = new Dictionary<string, CinematicNode>();
            foreach (var node in graph.allNodes)
            {
                if (node != null && !string.IsNullOrEmpty(node.id))
                {
                    nodeMap[node.id] = node;
                }
            }
            
            Debug.Log($"[CinematicGraphLibrary] Карта нод: {nodeMap.Count} entries");
            
            // Восстанавливаем связи
            int restoredCount = 0;
            foreach (var node in graph.allNodes)
            {
                if (node == null) continue;
                
                // Проверяем StartNode
                if (node is CinematicSystem.Nodes.StartNode startNode)
                {
                    // StartNode хранит nextNode по особому - нужно восстановить из allNodes по порядку
                    int nodeIndex = graph.allNodes.IndexOf(node);
                    Debug.Log($"[RestoreNodeLinks] StartNode найден: index={nodeIndex}, allNodes.Count={graph.allNodes.Count}");
                    if (nodeIndex >= 0 && nodeIndex < graph.allNodes.Count - 1)
                    {
                        var nextNodeInList = graph.allNodes[nodeIndex + 1];
                        Debug.Log($"[RestoreNodeLinks] nextNodeInList={nextNodeInList?.name ?? "null"}, startNode.nextNode={startNode.nextNode?.name ?? "null"}");
                        Debug.Log($"[RestoreNodeLinks] Условие: {startNode.nextNode != nextNodeInList} (current={startNode.nextNode?.name}, expected={nextNodeInList?.name})");
                        if (startNode.nextNode != nextNodeInList)
                        {
                            startNode.nextNode = nextNodeInList;
                            restoredCount++;
                            Debug.Log($"[CinematicGraphLibrary] Восстановлена связь StartNode -> {nextNodeInList?.name}");
                        }
                    }
                }
                // Проверяем NextNode (все кроме EndNode)
                else if (node is CinematicSystem.NextNode nextNode && !(node is CinematicSystem.Nodes.EndNode))
                {
                    // Для остальных нод пробуем восстановить связь по позиции в списке
                    int nodeIndex = graph.allNodes.IndexOf(node);
                    if (nodeIndex >= 0 && nodeIndex < graph.allNodes.Count - 1)
                    {
                        var nextNodeInList = graph.allNodes[nodeIndex + 1];
                        if (nextNode.nextNode != nextNodeInList)
                        {
                            nextNode.nextNode = nextNodeInList;
                            restoredCount++;
                        }
                    }
                }
            }
            
            Debug.Log($"[CinematicGraphLibrary] Восстановлено {restoredCount} связей");
        }
        
        /// <summary>
        /// Очистить кэш графов.
        /// </summary>
        public static void ClearCache()
        {
            graphCache.Clear();
        }
    }
}
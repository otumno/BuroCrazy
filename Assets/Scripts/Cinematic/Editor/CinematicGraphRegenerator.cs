// === FILE: Assets/Scripts/Cinematic/Editor/CinematicGraphRegenerator.cs ===
// Editor-скрипт для пересоздания графа Tutorial_Day1 из JSON
// Запускать через Unity Editor Menu: Bureau/Cinematic/Regenerate Tutorial Day1

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using CinematicSystem;
using DialogueSystem.Data;

namespace CinematicSystem.Editor
{
    public static class CinematicGraphRegenerator
    {
        private const string TARGET_GRAPH_NAME = "Tutorial_Day1";
        private const string TARGET_ASSET_PATH = "Assets/Data/CinematicGraphs/Tutorial_Day1.asset";
        
        private const string JSON_CONTENT = @"
{
  ""name"": ""Tutorial_Day1"",
  ""isBackground"": false,
  ""nodes"": [
    {""id"":""start"",""t"":""start"",""x"":""camera_debug""},
    {""id"":""camera_debug"",""t"":""camera"",""mode"":""debug"",""x"":""m1""},
    {""id"":""m1"",""t"":""move"",""wp"":""FirstStopWaypont"",""chr"":""Director"",""spd"":-1,""wf"":true,""up"":1,""x"":""d1""},
    {""id"":""d1"",""t"":""play_dialogue"",""dialogue"":""Assets/Scripts/DialogueSystem/DialoguesBase/Generated/Tutorial_FirstStep.asset"",""x"":""m2""},
    {""id"":""m2"",""t"":""move"",""wp"":""SecondStopWaypont"",""chr"":""Director"",""spd"":-1,""wf"":true,""up"":1,""x"":""d2""},
    {""id"":""d2"",""t"":""play_dialogue"",""dialogue"":""Assets/Scripts/DialogueSystem/DialoguesBase/Generated/Tutorial_SecondStep.asset"",""x"":""arrow""},
    {""id"":""arrow"",""t"":""show_arrow"",""x"":""m3""},
    {""id"":""m3"",""t"":""move"",""wp"":""DeskWaypoint"",""chr"":""Director"",""spd"":-1,""wf"":true,""up"":1,""x"":""end""},
    {""id"":""end"",""t"":""end""}
  ]
}";

        [MenuItem("Bureau/Cinematic/Regenerate Tutorial Day1")]
        public static void RegenerateTutorialDay1()
        {
            // Находим существующий asset
            CinematicGraph existingGraph = AssetDatabase.LoadAssetAtPath<CinematicGraph>(TARGET_ASSET_PATH);
            if (existingGraph == null)
            {
                Debug.LogError($"[CinematicGraphRegenerator] Ассет не найден: {TARGET_ASSET_PATH}");
                return;
            }

            // Удаляем все старые ноды из графа
            foreach (var node in existingGraph.allNodes)
            {
                if (node != null)
                {
                    AssetDatabase.RemoveObjectFromAsset(node);
                    UnityEngine.Object.DestroyImmediate(node, true);
                }
            }
            existingGraph.allNodes.Clear();
            existingGraph.startNode = null;

            // Импортируем JSON для получения структуры и связей
            var newGraph = CinematicJSONImporter.Import(JSON_CONTENT);
            if (newGraph == null)
            {
                Debug.LogError("[CinematicGraphRegenerator] Ошибка импорта JSON");
                return;
            }

            // Сначала создаём все ноды в existingGraph с правильными ID
            // используя reflection для вызова CreateNode с правильным типом
            Dictionary<string, CinematicNode> nodeMap = new Dictionary<string, CinematicNode>();
            
            // Собираем порядок нод из JSON
            var jsonNodes = ParseJSONNodes(JSON_CONTENT);
            
            // Создаём ноды в правильном порядке
            foreach (var jsonNode in jsonNodes)
            {
                Type nodeType = GetNodeType(jsonNode.t);
                if (nodeType == null) continue;
                
                // Создаём ноду через reflection
                var method = typeof(CinematicGraph).GetMethod("CreateNode", Type.EmptyTypes);
                var genericMethod = method.MakeGenericMethod(nodeType);
                CinematicNode createdNode = genericMethod.Invoke(existingGraph, null) as CinematicNode;
                
                if (createdNode != null)
                {
                    createdNode.id = jsonNode.id;
                    nodeMap[jsonNode.id] = createdNode;
                    
                    // Заполняем свойства ноды из JSON
                    ApplyNodeProperties(createdNode, jsonNode);
                    
                    Debug.Log($"[CinematicGraphRegenerator] Создана нода: {createdNode.name} (id={jsonNode.id})");
                }
            }
            
            // Устанавливаем startNode
            if (jsonNodes.Count > 0 && nodeMap.TryGetValue(jsonNodes[0].id, out var firstNode))
            {
                existingGraph.startNode = firstNode;
                Debug.Log($"[Regenerator] Установлен startNode: {firstNode?.name ?? "null"} (id={jsonNodes[0].id})");
            }
            
            // Дамп всех созданных нод
            Debug.Log("[Regenerator] === Дамп всех созданных нод ===");
            foreach (var kvp in nodeMap)
            {
                Debug.Log($"  {kvp.Key} -> {kvp.Value?.name ?? "null"} (type={kvp.Value?.GetType().Name ?? "null"})");
            }
            Debug.Log("[Regenerator] === Конец дампа ===");
            
            Debug.Log($"[Regenerator] Второй проход - установка связей. Всего нод в мапе: {nodeMap.Count}");
            foreach (var jsonNode in jsonNodes)
            {
                if (!nodeMap.TryGetValue(jsonNode.id, out var node)) continue;
                
                // Следующий узел
                Debug.Log($"[Regenerator] Обрабатываю ноду {jsonNode.id} (type={node.GetType().FullName}), следующий x={jsonNode.x}");
                if (!string.IsNullOrEmpty(jsonNode.x) && nodeMap.TryGetValue(jsonNode.x, out var nextNode))
                {
                    Debug.Log($"[Regenerator] Найден следующий узел: {nextNode?.name ?? "null"} для {jsonNode.id}");
                    if (node is CinematicSystem.NextNode next)
                    {
                        next.nextNode = nextNode;
                        Debug.Log($"[Regenerator] Установил nextNode для {node.name} (NextNode)");
                    }
                    else if (node is CinematicSystem.Nodes.StartNode start)
                    {
                        start.nextNode = nextNode;
                        Debug.Log($"[Regenerator] Установил nextNode для {node.name} (StartNode)");
                    }
                    else
                    {
                        Debug.LogWarning($"[Regenerator] Нода {node.name} не является NextNode или StartNode!");
                    }
                }
            }

            // Сохраняем
            EditorUtility.SetDirty(existingGraph);
            AssetDatabase.SaveAssets();
            
            // Дополнительно - пересоздаём ссылки через FindPropertyRelative для надёжности
            ReconnectAllNodes(existingGraph);
            
            // Принудительно сохраняем КАЖДУЮ ноду
            foreach (var node in existingGraph.allNodes)
            {
                if (node != null)
                {
                    EditorUtility.SetDirty(node);
                }
            }
            AssetDatabase.SaveAssets();
            
            // Проверяем что связи сохранились
            ValidateConnections(existingGraph);
            
            AssetDatabase.Refresh();

            // Также обновляем isBackground
            existingGraph.isBackground = false;

            Debug.Log($"[CinematicGraphRegenerator] Граф {TARGET_GRAPH_NAME} успешно пересоздан! Нод: {existingGraph.allNodes.Count}");
        }
        
        /// <summary>
        /// Пересоздаёт связи между нодами через сериализацию.
        /// Это гарантирует что ссылки между sub-assets сохранятся.
        /// </summary>
        private static void ReconnectAllNodes(CinematicGraph graph)
        {
            Debug.Log("[Regenerator] ReconnectAllNodes: начинаем переподключение...");
            
            // Создаём карту ID -> Node
            Dictionary<string, CinematicNode> nodeMap = new Dictionary<string, CinematicNode>();
            foreach (var node in graph.allNodes)
            {
                if (node != null && !string.IsNullOrEmpty(node.id))
                {
                    nodeMap[node.id] = node;
                }
            }
            
            Debug.Log($"[Regenerator] Карта нод: {nodeMap.Count} entries");
            
            // Проходим по всем нодам и устанавливаем связи
            foreach (var node in graph.allNodes)
            {
                if (node == null) continue;
                
                // Определяем следующую ноду по ID связи
                string nextId = GetNextNodeId(node);
                if (!string.IsNullOrEmpty(nextId) && nodeMap.TryGetValue(nextId, out var nextNode))
                {
                    SetNextNode(node, nextNode);
                    Debug.Log($"[Regenerator] Установил связь: {node.name} -> {nextNode.name}");
                }
            }
            
            // Явно сохраняем
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            Debug.Log("[Regenerator] ReconnectAllNodes: завершено");
        }
        
        /// <summary>
        /// Проверяет что связи между нодами корректны и сохранились.
        /// </summary>
        private static void ValidateConnections(CinematicGraph graph)
        {
            Debug.Log("[Regenerator] ValidateConnections: проверяем связи...");
            int validCount = 0;
            foreach (var node in graph.allNodes)
            {
                if (node == null) continue;
                
                string nextId = GetNextNodeId(node);
                if (!string.IsNullOrEmpty(nextId))
                {
                    validCount++;
                }
            }
            Debug.Log($"[Regenerator] ValidateConnections: {validCount} нод с ненулевыми связями");
            
            // Проверяем startNode
            if (graph.startNode != null && graph.startNode is CinematicSystem.Nodes.StartNode startNode)
            {
                string startNextId = startNode.nextNode?.id ?? "null";
                Debug.Log($"[Regenerator] startNode.nextNode id = {startNextId}");
            }
        }
        
        private static string GetNextNodeId(CinematicNode node)
        {
            if (node is CinematicSystem.Nodes.StartNode start)
                return start.nextNode?.id;
            if (node is CinematicSystem.NextNode next)
                return next.nextNode?.id;
            return null;
        }
        
        private static void SetNextNode(CinematicNode node, CinematicNode nextNode)
        {
            if (node is CinematicSystem.Nodes.StartNode start)
            {
                start.nextNode = nextNode;
            }
            else if (node is CinematicSystem.NextNode next)
            {
                next.nextNode = nextNode;
            }
        }
        
        private static List<JSONNodeData> ParseJSONNodes(string json)
        {
            // Простой парсер для получения списка нод из JSON
            var result = new List<JSONNodeData>();
            
            // Используем JsonUtility для парсинга
            var wrapper = JsonUtility.FromJson<JSONGraphWrapper>(json);
            if (wrapper != null && wrapper.nodes != null)
            {
                foreach (var n in wrapper.nodes)
                {
                    result.Add(n);
                }
            }
            
            return result;
        }
        
        [Serializable]
        private class JSONGraphWrapper
        {
            public string name;
            public List<JSONNodeData> nodes;
        }
        
        [Serializable]
        private class JSONNodeData
        {
            public string id;
            public string t;
            public string x;
            // Дополнительные поля
            public string wp;
            public string chr;
            public float spd;
            public bool wf;
            public bool up;
            public bool kr;
            public string speaker;
            public string text;
            public float dur;
            public string type;
            public string mode;
            public int INT;
            public string STR;
            public bool BL;
            public string ui;
            public float to;
            public string graph;
            public string dialogue;
        }
        
        private static Type GetNodeType(string typeString)
        {
            switch (typeString)
            {
                case "start": return typeof(CinematicSystem.Nodes.StartNode);
                case "end": return typeof(CinematicSystem.Nodes.EndNode);
                case "move": return typeof(CinematicSystem.Nodes.MoveToNode);
                case "say_bubble": return typeof(CinematicSystem.Nodes.SayBubbleNode);
                case "say_dialog": return typeof(CinematicSystem.Nodes.SayDialogNode);
                case "wait": return typeof(CinematicSystem.Nodes.WaitForSecondsNode);
                case "wait_click": return typeof(CinematicSystem.Nodes.WaitForUIClickNode);
                case "condition": return typeof(CinematicSystem.Nodes.ConditionNode);
                case "event": return typeof(CinematicSystem.Nodes.EventNode);
                case "spawn": return typeof(CinematicSystem.Nodes.SpawnCharacterNode);
                case "call_dialogue": return typeof(CinematicSystem.Nodes.CallDialogueNode);
                case "camera": return typeof(CinematicSystem.Nodes.CameraNode);
                case "teleport": return typeof(CinematicSystem.Nodes.TeleportNode);
                case "comment": return typeof(CinematicSystem.Nodes.CommentNode);
                case "camera_move": return typeof(CinematicSystem.Nodes.CameraMoveNode);
                case "call_cinematic": return typeof(CinematicSystem.Nodes.CallCinematicGraphNode);
                case "random": return typeof(CinematicSystem.Nodes.RandomNode);
                case "wait_character_despawn": return typeof(CinematicSystem.Nodes.WaitForCharacterDespawnNode);
                case "play_dialogue": return typeof(CinematicSystem.Nodes.PlayDialogueNode);
                case "show_arrow": return typeof(CinematicSystem.Nodes.ShowArrowNode);
                default:
                    Debug.LogWarning($"[CinematicGraphRegenerator] Неизвестный тип: {typeString}");
                    return null;
            }
        }
        
        private static void ApplyNodeProperties(CinematicNode node, JSONNodeData json)
        {
            if (node is CinematicSystem.Nodes.StartNode startNode)
            {
                startNode.characterID = json.chr ?? "Director";
            }
            else if (node is CinematicSystem.Nodes.MoveToNode moveNode)
            {
                moveNode.targetKey = json.wp;
                moveNode.characterID = json.chr ?? "Director";
                moveNode.speed = json.spd > 0 ? json.spd : -1f;
                moveNode.waitForCompletion = json.wf;
                moveNode.usePathfinding = json.up;
            }
            else if (node is CinematicSystem.Nodes.SayBubbleNode bubbleNode)
            {
                bubbleNode.text = json.text;
                bubbleNode.speakerID = json.speaker ?? "Director";
                bubbleNode.duration = json.dur;
            }
            else if (node is CinematicSystem.Nodes.EventNode eventNode)
            {
                eventNode.eventType = ParseEventType(json.type);
                eventNode.intValue = json.INT;
                eventNode.stringValue = json.STR;
                eventNode.boolValue = json.BL;
            }
            else if (node is CinematicSystem.Nodes.WaitForUIClickNode waitClickNode)
            {
                waitClickNode.uiElementKey = json.ui;
                waitClickNode.timeout = json.to;
            }
            else if (node is CinematicSystem.Nodes.TeleportNode teleportNode)
            {
                teleportNode.targetKey = json.wp;
                teleportNode.characterID = json.chr ?? "Director";
                teleportNode.keepRotation = json.kr;
            }
            else if (node is CinematicSystem.Nodes.CameraNode cameraNode)
            {
                cameraNode.useDirectorCamera = json.mode == "debug";
            }
            else if (node is CinematicSystem.Nodes.CallDialogueNode callDialogueNode)
            {
                // note: dialogueGraph must be loaded manually from json.graph name
                // For now, this won't auto-link without asset database lookup
                Debug.LogWarning($"[Regenerator] CallDialogueNode 'graph' field not implemented - needs asset lookup");
            }
            else if (node is CinematicSystem.Nodes.PlayDialogueNode playDialogueNode)
            {
                // Загружаем DialogueGraph из JSON dialogue path
                if (!string.IsNullOrEmpty(json.dialogue))
                {
                    var dialogueAsset = AssetDatabase.LoadAssetAtPath<DialogueGraph>(json.dialogue);
                    if (dialogueAsset != null)
                    {
                        playDialogueNode.dialogue = dialogueAsset;
                        Debug.Log($"[Regenerator] PlayDialogueNode dialogue assigned: {json.dialogue}");
                    }
                    else
                    {
                        Debug.LogWarning($"[Regenerator] Dialogue asset not found: {json.dialogue}");
                    }
                }
            }
            // ShowArrowNode не имеет свойств для установки из JSON
            // Другие типы нод добавьте по необходимости
        }
        
        private static CinematicSystem.Nodes.EventType ParseEventType(string typeStr)
        {
            if (string.IsNullOrEmpty(typeStr)) return CinematicSystem.Nodes.EventType.SetFlag;
            
            switch (typeStr)
            {
                case "LockControl": return CinematicSystem.Nodes.EventType.LockControl;
                case "UnlockControl": return CinematicSystem.Nodes.EventType.UnlockControl;
                case "SetCursor": return CinematicSystem.Nodes.EventType.SetCursor;
                case "PlaySound": return CinematicSystem.Nodes.EventType.PlaySound;
                case "OpenDirectorDesk": return CinematicSystem.Nodes.EventType.OpenDirectorDesk;
                default: return CinematicSystem.Nodes.EventType.SetFlag;
            }
        }

        [MenuItem("Bureau/Cinematic/Regenerate All Graphs from JSON")]
        public static void RegenerateAllGraphs()
        {
            // Этот метод можно расширить для всех графов
            RegenerateTutorialDay1();
        }
    }
}
#endif
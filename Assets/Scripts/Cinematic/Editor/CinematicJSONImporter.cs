// === FILE: Assets/Scripts/Cinematic/Editor/CinematicJSONImporter.cs ===
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using CinematicSystem.Nodes;

namespace CinematicSystem.Editor
{
    /// <summary>
    /// Импортер JSON для CinematicGraph.
    /// </summary>
    public static class CinematicJSONImporter
    {
        [Serializable]
        private class JSONNode
        {
            public string id;
            public string t; // тип
            public string x; // следующий узел
            // Поля для move
            public string wp;
            public string chr;
            public float spd;
            public bool wf;
            public bool up;
            // Поля для say_bubble
            public string speaker;
            public string text;
            public float dur;
            // Поля для say_dialog
            public string name;
            public string spr;
            // Поля для wait
            public float secs;
            public bool urt;
            // Поля для wait_click
            public string ui;
            public float to;
            // Поля для condition
            public string ck;
            public string op;
            public int val;
            // Поля для event
            public string type;
            public int INT;
            public string STR;
            public bool BL;
            public string tok;
            public string cid;
            // Поля для spawn
            public string arch;
            public string spwn;
            public string refk;
            public string goal;
            // Поля для call_dialogue
            public string graph;
            // Поля для camera
            public string tkey;
            public float osize;
            public float dur_cam;
            public bool udc;
            public bool wfc;
            // Поля для comment
            public string cmt;
            // Поля для teleport
            public string tkey_tp;
            // Поля для show_arrow
            public string arrow;
        }

        [Serializable]
        private class JSONGraph
        {
            public string name;
            public List<JSONNode> nodes;
        }

        /// <summary>
        /// Импортировать граф из JSON строки.
        /// </summary>
        /// <param name="json">JSON строка</param>
        /// <param name="ownerWindow">Окно редактора для сохранения (опционально)</param>
        /// <returns>Импортированный граф или null</returns>
        public static CinematicGraph Import(string json, CinematicEditorWindow ownerWindow = null)
        {
            try
            {
                var data = JsonUtility.FromJson<JSONGraph>(json);
                if (data == null || data.nodes == null)
                {
                    Debug.LogError("[CinematicJSONImporter] Неверный формат JSON");
                    return null;
                }

                // Создаём граф (не добавляем к assets для runtime импорта)
                var graph = ScriptableObject.CreateInstance<CinematicGraph>();
                graph.graphName = string.IsNullOrEmpty(data.name) ? "ImportedGraph" : data.name;
                graph.graphID = UnityEditor.GUID.Generate().ToString();

                // Создаём узлы через reflection (runtime-safe метод)
                Dictionary<string, CinematicNode> nodeMap = new Dictionary<string, CinematicNode>();

                foreach (var jsonNode in data.nodes)
                {
                    CinematicNode node = CreateNodeFromJSON(graph, jsonNode);
                    if (node != null)
                    {
                        nodeMap[jsonNode.id] = node;
                    }
                }

                // Устанавливаем связи
                foreach (var jsonNode in data.nodes)
                {
                    if (!nodeMap.TryGetValue(jsonNode.id, out var node)) continue;

                    // Следующий узел
                    if (!string.IsNullOrEmpty(jsonNode.x) && nodeMap.TryGetValue(jsonNode.x, out var next))
                    {
                        if (node is NextNode nextNode)
                        {
                            nextNode.nextNode = next;
                        }
                        else if (node is StartNode startNode)
                        {
                            startNode.nextNode = next;
                        }
                    }

                    // Специальные связи для ConditionNode
                    if (node is ConditionNode condNode)
                    {
                        // Ищем true и false узлы по соглашению об именах
                        string trueId = jsonNode.id + "_true";
                        string falseId = jsonNode.id + "_false";

                        if (nodeMap.TryGetValue(trueId, out var trueNode))
                            condNode.trueNode = trueNode;
                        if (nodeMap.TryGetValue(falseId, out var falseNode))
                            condNode.falseNode = falseNode;
                    }
                }

                // Устанавливаем стартовый узел
                if (data.nodes.Count > 0 && nodeMap.TryGetValue(data.nodes[0].id, out var firstNode))
                {
                    graph.startNode = firstNode;
                }

                // Примечание: Сохранение графа теперь происходит в CinematicEditorWindow (автосохранение)
                // Здесь только возвращаем граф без дополнительных диалогов
                
                return graph;
            }
            catch (Exception e)
            {
                Debug.LogError($"[CinematicJSONImporter] Ошибка импорта: {e.Message}\n{e.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Создать узел из JSON данных (runtime-safe, без AddObjectToAsset).
        /// </summary>
        private static CinematicNode CreateNodeFromJSON(CinematicGraph graph, JSONNode jsonNode)
        {
            CinematicNode node = null;
            Type nodeType = null;

            switch (jsonNode.t)
            {
                case "start":
                    nodeType = typeof(StartNode);
                    break;
                case "end":
                    nodeType = typeof(EndNode);
                    break;
                case "move":
                    nodeType = typeof(MoveToNode);
                    break;
                case "say_bubble":
                    nodeType = typeof(SayBubbleNode);
                    break;
                case "say_dialog":
                    nodeType = typeof(SayDialogNode);
                    break;
                case "wait":
                    nodeType = typeof(WaitForSecondsNode);
                    break;
                case "wait_click":
                    nodeType = typeof(WaitForUIClickNode);
                    break;
                case "condition":
                    nodeType = typeof(ConditionNode);
                    break;
                case "event":
                    nodeType = typeof(EventNode);
                    break;
                case "spawn":
                    nodeType = typeof(SpawnCharacterNode);
                    break;
                case "call_dialogue":
                    nodeType = typeof(CallDialogueNode);
                    break;
                case "camera":
                    nodeType = typeof(CameraNode);
                    break;
                case "teleport":
                    nodeType = typeof(TeleportNode);
                    break;
                case "comment":
                    nodeType = typeof(CommentNode);
                    break;
                case "show_arrow":
                    nodeType = typeof(ShowArrowNode);
                    break;
                default:
                    Debug.LogWarning($"[CinematicJSONImporter] Неизвестный тип узла: {jsonNode.t}");
                    return null;
            }

            // Создаём узел через reflection (runtime-safe)
            node = ScriptableObject.CreateInstance(nodeType) as CinematicNode;
            if (node == null) return null;

            // Присваиваем базовые свойства
            node.id = jsonNode.id;
            node.name = $"{jsonNode.t}_{jsonNode.id}";

            // Заполняем специфичные свойства
            switch (jsonNode.t)
            {
                case "start":
                    var startNode = node as StartNode;
                    if (startNode != null)
                    {
                        startNode.characterID = jsonNode.chr ?? "Director";
                    }
                    break;

                case "move":
                    var moveNode = node as MoveToNode;
                    if (moveNode != null)
                    {
                        moveNode.targetKey = jsonNode.wp;
                        moveNode.characterID = jsonNode.chr ?? "Director";
                        moveNode.speed = jsonNode.spd > 0 ? jsonNode.spd : -1f;
                        moveNode.waitForCompletion = jsonNode.wf;
                        moveNode.usePathfinding = jsonNode.up;
                    }
                    break;

                case "say_bubble":
                    var bubbleNode = node as SayBubbleNode;
                    if (bubbleNode != null)
                    {
                        bubbleNode.text = jsonNode.text;
                        bubbleNode.speakerID = jsonNode.speaker ?? "Director";
                        bubbleNode.duration = jsonNode.dur;
                    }
                    break;

                case "say_dialog":
                    var dialogNode = node as SayDialogNode;
                    if (dialogNode != null)
                    {
                        dialogNode.text = jsonNode.text;
                        dialogNode.speakerName = jsonNode.name ?? "Директор";
                        dialogNode.duration = jsonNode.dur;
                    }
                    break;

                case "wait":
                    var waitNode = node as WaitForSecondsNode;
                    if (waitNode != null)
                    {
                        waitNode.seconds = jsonNode.secs;
                        waitNode.useRealtime = jsonNode.urt;
                    }
                    break;

                case "wait_click":
                    var clickNode = node as WaitForUIClickNode;
                    if (clickNode != null)
                    {
                        clickNode.uiElementKey = jsonNode.ui;
                        clickNode.timeout = jsonNode.to;
                    }
                    break;

                case "condition":
                    var condNode = node as ConditionNode;
                    if (condNode != null)
                    {
                        condNode.conditionKey = jsonNode.ck;
                        condNode.operation = jsonNode.op ?? "==";
                        condNode.value = jsonNode.val;
                    }
                    break;

                case "event":
                    var eventNode = node as EventNode;
                    if (eventNode != null)
                    {
                        if (Enum.TryParse<Nodes.EventType>(jsonNode.type, out var eventType))
                            eventNode.eventType = eventType;
                        eventNode.intValue = jsonNode.INT;
                        eventNode.stringValue = jsonNode.STR;
                        eventNode.boolValue = jsonNode.BL;
                        eventNode.targetObjectKey = jsonNode.tok;
                        eventNode.characterID = jsonNode.cid ?? "Director";
                    }
                    break;

                case "spawn":
                    var spawnNode = node as SpawnCharacterNode;
                    if (spawnNode != null)
                    {
                        spawnNode.archetypeID = jsonNode.arch;
                        spawnNode.spawnPointKey = jsonNode.spwn;
                        spawnNode.targetKeyForReference = jsonNode.refk;
                        if (Enum.TryParse<ClientGoal>(jsonNode.goal, out var goal))
                            spawnNode.forcedGoal = goal;
                    }
                    break;

                case "call_dialogue":
                    var callNode = node as CallDialogueNode;
                    if (callNode != null)
                    {
                        callNode.dialogueGraph = FindDialogueGraph(jsonNode.graph);
                    }
                    break;

                case "camera":
                    var cameraNode = node as CameraNode;
                    if (cameraNode != null)
                    {
                        cameraNode.targetKey = jsonNode.tkey;
                        cameraNode.orthographicSize = jsonNode.osize;
                        cameraNode.duration = jsonNode.dur_cam;
                        cameraNode.useDirectorCamera = jsonNode.udc;
                        cameraNode.waitForCompletion = jsonNode.wfc;
                    }
                    break;

                case "teleport":
                    var tpNode = node as TeleportNode;
                    if (tpNode != null)
                    {
                        tpNode.targetKey = jsonNode.tkey_tp;
                        tpNode.characterID = jsonNode.chr ?? "Director";
                    }
                    break;

                case "comment":
                    var commentNode = node as CommentNode;
                    if (commentNode != null)
                    {
                        commentNode.comment = jsonNode.cmt ?? "";
                    }
                    break;

                case "show_arrow":
                    var arrowNode = node as ShowArrowNode;
                    if (arrowNode != null)
                    {
                        if (!string.IsNullOrEmpty(jsonNode.arrow))
                            arrowNode.arrowKey = jsonNode.arrow;
                    }
                    break;
            }

            // Добавляем в список графа
            graph.allNodes.Add(node);

            return node;
        }

        /// <summary>
        /// Найти DialogueGraph по имени.
        /// </summary>
        private static DialogueSystem.Data.DialogueGraph FindDialogueGraph(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var guids = UnityEditor.AssetDatabase.FindAssets($"t:DialogueGraph {name}");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                return UnityEditor.AssetDatabase.LoadAssetAtPath<DialogueSystem.Data.DialogueGraph>(path);
            }

            return null;
        }
        
        // === Авто-размещение узлов при импорте ===
        
        /// <summary>
        /// Автоматически рассчитать позиции всех узлов в графе (DFS обход).
        /// Вызывается после импорта JSON для автоматической раскладки узлов.
        /// </summary>
        /// <param name="graph">Граф для расстановки</param>
        public static void AutoLayoutNodes(CinematicGraph graph)
        {
            if (graph == null || graph.startNode == null) return;
            
            var visited = new HashSet<string>();
            float startX = 50;
            float startY = 200; // Центр по вертикали
            float nodeWidth = 200;
            float nodeHeight = 150;
            float xOffset = 280; // Горизонтальный отступ между уровнями (с промежутком 80)
            float yOffsetBranch = 180; // Вертикальный отступ для ветвей Condition
            
            // DFS обход графа с расстановкой позиций
            void ArrangeNode(CinematicNode node, float x, float y)
            {
                if (node == null || visited.Contains(node.id)) return;
                visited.Add(node.id);
                
                // Устанавливаем позицию узла
                node.editorPosition = new Rect(x, y, nodeWidth, nodeHeight);
                
                // Определяем следующий узел для разных типов
                // StartNode имеет своё поле nextNode (не NextNode!)
                if (node is StartNode startNode && startNode.nextNode != null)
                {
                    ArrangeNode(startNode.nextNode, x + xOffset, y);
                }
                // NextNode - линейная цепочка идёт горизонтально вправо
                else if (node is NextNode nextNode && nextNode.nextNode != null)
                {
                    ArrangeNode(nextNode.nextNode, x + xOffset, y);
                }
                
                // ConditionNode - ветвление (оба потомка смещаются вправо и вверх/вниз)
                if (node is ConditionNode condNode)
                {
                    // True ветвь идёт вниз по Y
                    if (condNode.trueNode != null && !visited.Contains(condNode.trueNode.id))
                    {
                        ArrangeNode(condNode.trueNode, x + xOffset, y + yOffsetBranch);
                    }
                    
                    // False ветвь идёт вверх по Y
                    if (condNode.falseNode != null && !visited.Contains(condNode.falseNode.id))
                    {
                        ArrangeNode(condNode.falseNode, x + xOffset, y - yOffsetBranch);
                    }
                }
            }
            
            ArrangeNode(graph.startNode, startX, startY);
            
            Debug.Log($"[CinematicJSONImporter] Авто-расстановка завершена. Размещено {visited.Count} узлов.");
        }
    }
}
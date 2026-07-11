// Assets/Editor/DialogueVerboseImporter.cs
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using DialogueSystem.Data;

namespace DialogueSystem.EditorTools
{
    /// <summary>
    /// Импортёр диалогов в «человеко-читаемом» JSON-формате для генератора контента (DeepSeek и т.п.).
    /// В отличие от компактного DialogueJSONImporter, здесь используются полные имена полей и
    /// стабильные строковые nodeID, по которым строятся связи между нодами.
    ///
    /// Формат JSON:
    /// {
    ///   "name": "Orwell_Day1",
    ///   "root": "start",
    ///   "nodes": [
    ///     { "nodeID": "start",  "type": "start",   "nextNodeID": "intro", "dialogueType": "World" },
    ///     { "nodeID": "intro",  "type": "phrase",  "speakerID": "Client", "text": "...", "variantTexts": ["...", "..."], "nextNodeID": "choice_main" },
    ///     { "nodeID": "choice_main", "type": "choice", "queryText": "Что ответить?",
    ///       "options": [
    ///         { "text": "Согласиться", "nextNodeID": "accept", "conditionKey": "Met_Inspector", "operation": "==", "conditionValue": 1 },
    ///         { "text": "Отказать",    "nextNodeID": "refuse" }
    ///       ]
    ///     },
    ///     { "nodeID": "accept", "type": "event", "eventType": "SetFlag", "flagKey": "Arc_Orwell_Stage0_Done", "intValue": 1, "nextNodeID": "end" },
    ///     { "nodeID": "refuse", "type": "phrase", "speakerID": "Client", "text": "Жаль...", "nextNodeID": "end" },
    ///     { "nodeID": "end",    "type": "end",    "outcome": "LeaveHappy", "outputEmotion": "Neutral", "stressModifier": 0 }
    ///   ]
    /// }
    /// </summary>
    public class DialogueVerboseImporter
    {
        [System.Serializable]
        public class GraphDto
        {
            public string name;
            public string root;
            public List<NodeDto> nodes;
            /// <summary>Asset path к Sprite, который будет установлен как defaultBackground стартовой ноды.</summary>
            public string defaultBackgroundResource;
        }

        [System.Serializable]
        public class NodeDto
        {
            public string nodeID;
            public string type;

            // Phrase / Event
            public string speakerID;
            public string text;
            public string[] variantTexts;

            // Choice
            public string queryText;
            public List<OptionDto> options;

            // Event
            public string eventType;
            public string flagKey;
            public int intValue;
            public string notificationText;

            // Condition
            public string conditionKey;
            public string operation;
            public int conditionValue;
            public string trueNodeID;
            public string falseNodeID;

            // Common links
            public string nextNodeID;

            // Random (uses options with chance)
            // (option.chance is 0..100)

            // Start
            public string dialogueType;

            // End
            public string outcome;
            public string outputEmotion;
            public float stressModifier;
        }

        [System.Serializable]
        public class OptionDto
        {
            public string text;
            public string nextNodeID;
            public string conditionKey;
            public string operation;
            public int conditionValue;
            public float chance; // для Random: 0..100
        }

        /// <summary>
        /// Импортировать JSON в файл ассета DialogueGraph по пути assetPath (например, "Assets/Foo.asset").
        /// </summary>
        public static DialogueGraph Import(string json, string assetPath)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentException("JSON пустой.", nameof(json));
            if (string.IsNullOrEmpty(assetPath))
                throw new ArgumentException("Не указан путь ассета.", nameof(assetPath));

            GraphDto data;
            try { data = JsonUtility.FromJson<GraphDto>(json); }
            catch (Exception ex) { throw new Exception("Ошибка парсинга JSON: " + ex.Message, ex); }

            if (data == null || string.IsNullOrEmpty(data.name))
                throw new Exception("JSON не содержит поле name или повреждён.");
            if (data.nodes == null)
                throw new Exception("JSON не содержит массив nodes.");

            EnsureFolder(assetPath);

            DialogueGraph graph = AssetDatabase.LoadAssetAtPath<DialogueGraph>(assetPath);
            bool created = false;
            if (graph == null)
            {
                graph = ScriptableObject.CreateInstance<DialogueGraph>();
                AssetDatabase.CreateAsset(graph, assetPath);
                created = true;
            }
            else
            {
                // Очищаем старые subassets, чтобы не накапливать дубликаты при повторном импорте.
                ClearSubAssets(graph);
            }

            var idMap = new Dictionary<string, DialogueNode>();
            var links = new List<LinkReq>();

            foreach (var n in data.nodes)
            {
                if (n == null) continue;
                if (string.IsNullOrEmpty(n.nodeID))
                {
                    Debug.LogWarning($"[DialogueVerboseImporter] Узел без nodeID пропущен (type={n.type}).");
                    continue;
                }

                DialogueNode node = CreateNode(graph, n, links);
                if (node == null) continue;

                node.name = $"{n.type}_{n.nodeID}";
                node.nodeID = n.nodeID;

                idMap[n.nodeID] = node;
                EditorUtility.SetDirty(node);
            }

            // Стартовая нода
            if (!string.IsNullOrEmpty(data.root) && idMap.TryGetValue(data.root, out var rootNode))
            {
                if (rootNode is StartNode sn)
                {
                    graph.startNode = sn;
                }
                else
                {
                    // Если root указывает не на StartNode — создаём обёртку.
                    var autoStart = graph.CreateNode<StartNode>();
                    autoStart.name = "start_auto";
                    autoStart.nodeID = "__auto_start";
                    autoStart.nextNode = rootNode;
                    graph.startNode = autoStart;
                    EditorUtility.SetDirty(autoStart);
                }
            }

            // [ИСПРАВЛЕНО] Применяем фон диалога к стартовой ноде.
            ApplyDefaultBackground(graph, data.defaultBackgroundResource);

            // Линковка
            foreach (var l in links)
            {
                if (string.IsNullOrEmpty(l.nextId) || !idMap.TryGetValue(l.nextId, out var target))
                    continue;

                ApplyLink(l, target);
                EditorUtility.SetDirty(l.src);
            }

            EditorUtility.SetDirty(graph);

            // Финализируем subassets (один общий SaveAssets вместо N вызовов).
            DialogueGraph.FlushPendingSubassets();
            AssetDatabase.Refresh();

            Debug.Log($"[DialogueVerboseImporter] Граф '{data.name}' импортирован: {idMap.Count} узлов ({(created ? "создан" : "обновлён")}) → {assetPath}");
            return graph;
        }

        /// <summary>
        /// Импорт из файла на диске.
        /// </summary>
        public static DialogueGraph ImportFromFile(string jsonPath, string assetPath)
        {
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException("Файл не найден.", jsonPath);
            string json = File.ReadAllText(jsonPath);
            return Import(json, assetPath);
        }

        // ---------- internal ----------

        private class LinkReq
        {
            public DialogueNode src;
            public string nextId;
            public int optIndex = -1;
            public bool isTrueBranch;
        }

        private static DialogueNode CreateNode(DialogueGraph graph, NodeDto n, List<LinkReq> links)
        {
            switch ((n.type ?? "").ToLowerInvariant())
            {
                case "start":
                {
                    var s = graph.CreateNode<StartNode>();
                    if (!string.IsNullOrEmpty(n.dialogueType) && Enum.TryParse<DialogueType>(n.dialogueType, true, out var dt))
                        s.dialogueType = dt;
                    if (!string.IsNullOrEmpty(n.nextNodeID))
                        links.Add(new LinkReq { src = s, nextId = n.nextNodeID });
                    return s;
                }
                case "phrase":
                {
                    var p = graph.CreateNode<PhraseNode>();
                    p.speakerID = n.speakerID;
                    p.text = n.text;
                    if (n.variantTexts != null && n.variantTexts.Length > 0)
                        p.variantTexts = new List<string>(n.variantTexts);
                    if (!string.IsNullOrEmpty(n.nextNodeID))
                        links.Add(new LinkReq { src = p, nextId = n.nextNodeID });
                    return p;
                }
                case "choice":
                {
                    var c = graph.CreateNode<ChoiceNode>();
                    c.queryText = n.queryText;
                    if (n.options != null)
                    {
                        for (int i = 0; i < n.options.Count; i++)
                        {
                            var opt = n.options[i];
                            if (opt == null) continue;
                            var o = new ChoiceNode.ChoiceOption
                            {
                                text = opt.text,
                                conditionKey = opt.conditionKey,
                                operation = opt.operation,
                                conditionValue = opt.conditionValue
                            };
                            c.options.Add(o);
                            if (!string.IsNullOrEmpty(opt.nextNodeID))
                                links.Add(new LinkReq { src = c, nextId = opt.nextNodeID, optIndex = c.options.Count - 1 });
                        }
                    }
                    return c;
                }
                case "event":
                {
                    var e = graph.CreateNode<EventNode>();
                    if (!string.IsNullOrEmpty(n.eventType) && Enum.TryParse<EventNode.EventType>(n.eventType, true, out var et))
                        e.eventType = et;
                    e.flagKey = n.flagKey;
                    e.intValue = n.intValue;
                    e.notificationText = n.notificationText;
                    if (!string.IsNullOrEmpty(n.nextNodeID))
                        links.Add(new LinkReq { src = e, nextId = n.nextNodeID });
                    return e;
                }
                case "condition":
                {
                    var c = graph.CreateNode<ConditionNode>();
                    c.conditionKey = n.conditionKey;
                    c.operation = n.operation;
                    c.conditionValue = n.conditionValue;
                    if (!string.IsNullOrEmpty(n.trueNodeID))
                        links.Add(new LinkReq { src = c, nextId = n.trueNodeID, isTrueBranch = true });
                    if (!string.IsNullOrEmpty(n.falseNodeID))
                        links.Add(new LinkReq { src = c, nextId = n.falseNodeID, isTrueBranch = false });
                    return c;
                }
                case "random":
                {
                    var r = graph.CreateNode<RandomNode>();
                    if (n.options != null)
                    {
                        for (int i = 0; i < n.options.Count; i++)
                        {
                            var opt = n.options[i];
                            if (opt == null) continue;
                            var ro = new RandomNode.RandomOutcome { chance = Mathf.Clamp01(opt.chance / 100f) };
                            r.outcomes.Add(ro);
                            if (!string.IsNullOrEmpty(opt.nextNodeID))
                                links.Add(new LinkReq { src = r, nextId = opt.nextNodeID, optIndex = r.outcomes.Count - 1 });
                        }
                    }
                    return r;
                }
                case "end":
                {
                    var e = graph.CreateNode<EndNode>();
                    if (!string.IsNullOrEmpty(n.outcome) && Enum.TryParse<EndNode.DialogueOutcome>(n.outcome, true, out var oc))
                        e.outcome = oc;
                    if (!string.IsNullOrEmpty(n.outputEmotion) && Enum.TryParse<Emotion>(n.outputEmotion, true, out var em))
                        e.outputEmotion = em;
                    e.stressModifier = n.stressModifier;
                    return e;
                }
                default:
                    Debug.LogWarning($"[DialogueVerboseImporter] Неизвестный тип ноды '{n.type}' (nodeID={n.nodeID}). Пропускаю.");
                    return null;
            }
        }

        private static void ApplyLink(LinkReq l, DialogueNode target)
        {
            switch (l.src)
            {
                case StartNode sn: sn.nextNode = target; break;
                case PhraseNode pn: pn.nextNode = target; break;
                case EventNode en: en.nextNode = target; break;
                case ChoiceNode cn when l.optIndex >= 0 && l.optIndex < cn.options.Count:
                    cn.options[l.optIndex].nextNode = target; break;
                case RandomNode rn when l.optIndex >= 0 && l.optIndex < rn.outcomes.Count:
                    rn.outcomes[l.optIndex].nextNode = target; break;
                case ConditionNode cond:
                    if (l.isTrueBranch) cond.trueNode = target; else cond.falseNode = target; break;
            }
        }

        private static void EnsureFolder(string assetPath)
        {
            string dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(dir) || AssetDatabase.IsValidFolder(dir)) return;
            // Рекурсивно создаём недостающие папки.
            string[] parts = dir.Split('/');
            string accum = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = accum + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(accum, parts[i]);
                accum = next;
            }
        }

        private static void ClearSubAssets(DialogueGraph graph)
        {
            if (graph == null || graph.allNodes == null) return;
            var old = new List<DialogueNode>(graph.allNodes);
            graph.allNodes.Clear();
            graph.startNode = null;
            foreach (var n in old)
            {
                if (n == null) continue;
                if (AssetDatabase.IsSubAsset(n))
                {
                    string path = AssetDatabase.GetAssetPath(n);
                    if (!string.IsNullOrEmpty(path))
                        AssetDatabase.RemoveObjectFromAsset(n);
                }
                    UnityEngine.Object.DestroyImmediate(n, true);
            }
            EditorUtility.SetDirty(graph);
        }

        /// <summary>
        /// Назначает defaultBackground стартовой ноде DialogueGraph.
        /// Если resourcePath задан — пытается загрузить Sprite и применить.
        /// Если пусто — загружает fallback 'Assets/Sprites/Backs/DirectorOfficeBack.png'.
        /// Если ничего не найдено — ничего не делает (DialogueUIManager сам установит свой default в таком случае).
        /// </summary>
        private static void ApplyDefaultBackground(DialogueGraph graph, string resourcePath)
        {
            if (graph == null || graph.startNode == null) return;

            Sprite sprite = null;
            if (!string.IsNullOrEmpty(resourcePath))
            {
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(resourcePath);
                if (sprite == null)
                {
                    Debug.LogWarning($"[DialogueVerboseImporter] Background not found at '{resourcePath}' — falling back to DirectorOfficeBack.");
                }
            }
            if (sprite == null)
            {
                const string FallbackPath = "Assets/Sprites/Backs/DirectorOfficeBack.png";
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FallbackPath);
            }
            if (sprite == null) return;

            // startNode объявлен как DialogueNode (базовый тип), но defaultBackground
            // существует только на StartNode — приводим тип.
            if (graph.startNode is StartNode startNode)
            {
                startNode.defaultBackground = sprite;
                EditorUtility.SetDirty(startNode);
            }
        }
    }
}

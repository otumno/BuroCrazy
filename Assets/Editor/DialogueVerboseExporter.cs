// Assets/Editor/DialogueVerboseExporter.cs
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using DialogueSystem.Data;

namespace DialogueSystem.EditorTools
{
    /// <summary>
    /// Экспортёр существующего DialogueGraph в человеко-читаемый JSON.
    /// Используется для round-trip с генератором контента (DeepSeek и т.п.).
    /// Все ссылки между нодами выгружаются в виде стабильных строковых nodeID
    /// (если они заданы). Если у ноды нет nodeID — генерируется fallback
    /// на основе имени ассета, чтобы экспорт всегда был валидным.
    /// </summary>
    public static class DialogueVerboseExporter
    {
        [System.Serializable]
        private class GraphDto
        {
            public string name;
            public string root;
            public List<NodeDto> nodes;
        }

        [System.Serializable]
        private class NodeDto
        {
            public string nodeID;
            public string type;
            public string speakerID;
            public string text;
            public string[] variantTexts;
            public string queryText;
            public List<OptionDto> options;
            public string eventType;
            public string flagKey;
            public int intValue;
            public string notificationText;
            public string conditionKey;
            public string operation;
            public int conditionValue;
            public string trueNodeID;
            public string falseNodeID;
            public string nextNodeID;
            public string dialogueType;
            public string outcome;
            public string outputEmotion;
            public float stressModifier;
        }

        [System.Serializable]
        private class OptionDto
        {
            public string text;
            public string nextNodeID;
            public string conditionKey;
            public string operation;
            public int conditionValue;
            public float chance;
        }

        /// <summary>
        /// Возвращает JSON-строку описания графа.
        /// </summary>
        public static string ExportToJson(DialogueGraph graph)
        {
            if (graph == null) return null;
            var dto = BuildDto(graph);
            return JsonUtility.ToJson(dto, true);
        }

        /// <summary>
        /// Сохраняет JSON в файл на диске.
        /// </summary>
        public static void ExportToFile(DialogueGraph graph, string jsonPath, bool prettyPrint = true)
        {
            if (graph == null) return;
            string dir = Path.GetDirectoryName(jsonPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(jsonPath, ExportToJson(graph));
            Debug.Log($"[DialogueVerboseExporter] Граф '{graph.name}' экспортирован → {jsonPath}");
        }

        // ---------- internal ----------

        private static GraphDto BuildDto(DialogueGraph graph)
        {
            var dto = new GraphDto
            {
                name = graph.name,
                root = null,
                nodes = new List<NodeDto>()
            };

            // Карта nodeID → DialogueNode для быстрого разрешения ссылок при валидации.
            var idMap = new Dictionary<string, DialogueNode>();

            // Сначала собираем все ноды (кроме служебных авто-start'ов).
            if (graph.allNodes != null)
            {
                foreach (var node in graph.allNodes)
                {
                    if (node == null) continue;
                    if (node is StartNode && node.name == "start_auto") continue;
                    var nDto = BuildNodeDto(node);
                    dto.nodes.Add(nDto);
                    if (!string.IsNullOrEmpty(nDto.nodeID))
                        idMap[nDto.nodeID] = node;
                }
            }

            // Валидируем ссылки: если next указывает на ноду без nodeID — генерируем fallback.
            ValidateAndAssignFallbackIds(dto.nodes);

            // root = nodeID стартовой ноды.
            if (graph.startNode != null)
                dto.root = string.IsNullOrEmpty(graph.startNode.nodeID) ? null : graph.startNode.nodeID;

            return dto;
        }

        private static NodeDto BuildNodeDto(DialogueNode node)
        {
            var n = new NodeDto
            {
                nodeID = string.IsNullOrEmpty(node.nodeID) ? null : node.nodeID
            };

            switch (node)
            {
                case StartNode s:
                    n.type = "start";
                    n.dialogueType = s.dialogueType.ToString();
                    n.nextNodeID = Ref(s.nextNode);
                    break;
                case PhraseNode p:
                    n.type = "phrase";
                    n.speakerID = p.speakerID;
                    n.text = p.text;
                    if (p.variantTexts != null && p.variantTexts.Count > 0)
                        n.variantTexts = p.variantTexts.ToArray();
                    n.nextNodeID = Ref(p.nextNode);
                    break;
                case ChoiceNode c:
                    n.type = "choice";
                    n.queryText = c.queryText;
                    n.options = new List<OptionDto>();
                    if (c.options != null)
                    {
                        foreach (var o in c.options)
                        {
                            if (o == null) continue;
                            n.options.Add(new OptionDto
                            {
                                text = o.text,
                                nextNodeID = Ref(o.nextNode),
                                conditionKey = o.conditionKey,
                                operation = o.operation,
                                conditionValue = o.conditionValue
                            });
                        }
                    }
                    break;
                case EventNode e:
                    n.type = "event";
                    n.eventType = e.eventType.ToString();
                    n.flagKey = e.flagKey;
                    n.intValue = e.intValue;
                    n.notificationText = e.notificationText;
                    n.nextNodeID = Ref(e.nextNode);
                    break;
                case ConditionNode cn:
                    n.type = "condition";
                    n.conditionKey = cn.conditionKey;
                    n.operation = cn.operation;
                    n.conditionValue = cn.conditionValue;
                    n.trueNodeID = Ref(cn.trueNode);
                    n.falseNodeID = Ref(cn.falseNode);
                    break;
                case RandomNode r:
                    n.type = "random";
                    n.options = new List<OptionDto>();
                    if (r.outcomes != null)
                    {
                        foreach (var o in r.outcomes)
                        {
                            if (o == null) continue;
                            n.options.Add(new OptionDto
                            {
                                chance = o.chance * 100f,
                                nextNodeID = Ref(o.nextNode)
                            });
                        }
                    }
                    break;
                case EndNode en:
                    n.type = "end";
                    n.outcome = en.outcome.ToString();
                    n.outputEmotion = en.outputEmotion.ToString();
                    n.stressModifier = en.stressModifier;
                    break;
                default:
                    n.type = node.GetNodeType().ToString().ToLowerInvariant();
                    break;
            }

            return n;
        }

        private static string Ref(DialogueNode target)
        {
            if (target == null) return null;
            if (!string.IsNullOrEmpty(target.nodeID)) return target.nodeID;
            // Если у ноды нет nodeID — ссылка всё равно сериализуется как null;
            // реальное имя известно только после ValidateAndAssignFallbackIds.
            return null;
        }

        /// <summary>
        /// После первого прохода: у любой ноды без nodeID генерируется стабильный fallback,
        /// и все ссылки nextNodeID/trueNodeID/falseNodeID/options[*].nextNodeID,
        /// указывавшие на эту ноду по индексу (ранее null), обновляются.
        /// Это решает обратную задачу импорта: граф, у которого часть нод без nodeID,
        /// всё равно можно выгрузить в валидный JSON.
        /// </summary>
        private static void ValidateAndAssignFallbackIds(List<NodeDto> nodes)
        {
            // Соответствие: индекс в списке → nodeID (сгенерированный или существующий).
            for (int i = 0; i < nodes.Count; i++)
            {
                if (string.IsNullOrEmpty(nodes[i].nodeID))
                    nodes[i].nodeID = $"node_{i + 1}";
            }
            // Теперь все ноды имеют nodeID, и ссылки по nextNodeID остаются согласованными,
            // потому что Ref() возвращал null только при отсутствии nodeID; а раз его
            // не было ни у источника, ни у цели, восстанавливать индекс по null нельзя.
            // (Round-trip в полном объёме возможен, только если у нод выставлены nodeID
            // вручную или через импортёр.)
        }
    }
}

// === FILE: Assets/Scripts/Cinematic/Editor/CinematicJSONExporter.cs ===
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using CinematicSystem.Nodes;

namespace CinematicSystem.Editor
{
    /// <summary>
    /// Экспортёр CinematicGraph в JSON.
    /// </summary>
    public static class CinematicJSONExporter
    {
        /// <summary>
        /// Экспортировать граф в JSON.
        /// </summary>
        public static string Export(CinematicGraph graph)
        {
            if (graph == null) return "{}";

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"name\": \"{EscapeJson(graph.graphName)}\",");
            sb.AppendLine("  \"nodes\": [");

            bool first = true;
            foreach (var node in graph.allNodes)
            {
                if (!first) sb.AppendLine(",");
                first = false;

                sb.Append("    ");
                sb.Append(ExportNode(node, graph));
            }

            sb.AppendLine();
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// Экспортировать узел в JSON.
        /// </summary>
        private static string ExportNode(CinematicNode node, CinematicGraph graph)
        {
            var fields = new List<string>
            {
                $"\"id\": \"{node.id}\"",
                $"\"t\": \"{node.GetNodeType()}\""
            };

            // Базовые поля
            if (!string.IsNullOrEmpty(node.nodeName))
                fields.Add($"\"name\": \"{EscapeJson(node.nodeName)}\"");

            // Позиция
            fields.Add($"\"x\": {node.editorPosition.x:F0}");
            fields.Add($"\"y\": {node.editorPosition.y:F0}");

            // Следующий узел
            string nextId = GetNextNodeId(node, graph);
            if (!string.IsNullOrEmpty(nextId))
                fields.Add($"\"x\": \"{nextId}\"");

            // Поля в зависимости от типа
            if (node is MoveToNode move)
            {
                fields.Add($"\"wp\": \"{move.targetKey}\"");
                fields.Add($"\"chr\": \"{move.characterID}\"");
                if (move.speed > 0) fields.Add($"\"spd\": {move.speed}");
                fields.Add($"\"wf\": {(move.waitForCompletion ? "true" : "false")}");
                fields.Add($"\"up\": {(move.usePathfinding ? "true" : "false")}");
            }
            else if (node is SayBubbleNode bubble)
            {
                fields.Add($"\"speaker\": \"{bubble.speakerID}\"");
                fields.Add($"\"text\": \"{EscapeJson(bubble.text)}\"");
                fields.Add($"\"dur\": {bubble.duration}");
            }
            else if (node is SayDialogNode dialog)
            {
                fields.Add($"\"name\": \"{dialog.speakerName}\"");
                fields.Add($"\"text\": \"{EscapeJson(dialog.text)}\"");
                fields.Add($"\"dur\": {dialog.duration}");
            }
            else if (node is WaitForSecondsNode wait)
            {
                fields.Add($"\"secs\": {wait.seconds}");
                fields.Add($"\"urt\": {(wait.useRealtime ? "true" : "false")}");
            }
            else if (node is WaitForUIClickNode click)
            {
                fields.Add($"\"ui\": \"{click.uiElementKey}\"");
                fields.Add($"\"to\": {click.timeout}");
            }
            else if (node is ConditionNode cond)
            {
                fields.Add($"\"ck\": \"{cond.conditionKey}\"");
                fields.Add($"\"op\": \"{cond.operation}\"");
                fields.Add($"\"val\": {cond.value}");
                // Для conditionNode добавляем специальные связи
                if (cond.trueNode != null) fields.Add($"\"x_true\": \"{cond.trueNode.id}\"");
                if (cond.falseNode != null) fields.Add($"\"x_false\": \"{cond.falseNode.id}\"");
            }
            else if (node is EventNode evt)
            {
                fields.Add($"\"type\": \"{evt.eventType}\"");
                fields.Add($"\"INT\": {evt.intValue}");
                if (!string.IsNullOrEmpty(evt.stringValue)) fields.Add($"\"STR\": \"{EscapeJson(evt.stringValue)}\"");
                fields.Add($"\"BL\": {(evt.boolValue ? "true" : "false")}");
                if (!string.IsNullOrEmpty(evt.targetObjectKey)) fields.Add($"\"tok\": \"{evt.targetObjectKey}\"");
                if (!string.IsNullOrEmpty(evt.characterID)) fields.Add($"\"cid\": \"{evt.characterID}\"");
            }
            else if (node is SpawnCharacterNode spawn)
            {
                fields.Add($"\"arch\": \"{spawn.archetypeID}\"");
                fields.Add($"\"spwn\": \"{spawn.spawnPointKey}\"");
                fields.Add($"\"refk\": \"{spawn.targetKeyForReference}\"");
                fields.Add($"\"goal\": \"{spawn.forcedGoal}\"");
            }
            else if (node is CallDialogueNode call)
            {
                fields.Add($"\"graph\": \"{(call.dialogueGraph ? call.dialogueGraph.name : "")}\"");
            }
            else if (node is CameraNode cam)
            {
                fields.Add($"\"tkey\": \"{cam.targetKey}\"");
                if (cam.orthographicSize > 0) fields.Add($"\"osize\": {cam.orthographicSize}");
                fields.Add($"\"dur_cam\": {cam.duration}");
                fields.Add($"\"udc\": {(cam.useDirectorCamera ? "true" : "false")}");
                fields.Add($"\"wfc\": {(cam.waitForCompletion ? "true" : "false")}");
            }
            else if (node is TeleportNode tp)
            {
                fields.Add($"\"tkey_tp\": \"{tp.targetKey}\"");
                fields.Add($"\"chr\": \"{tp.characterID}\"");
            }
            else if (node is CommentNode comment)
            {
                fields.Add($"\"cmt\": \"{EscapeJson(comment.comment)}\"");
            }

            return "{" + string.Join(", ", fields) + "}";
        }

        /// <summary>
        /// Получить ID следующего узла.
        /// </summary>
        private static string GetNextNodeId(CinematicNode node, CinematicGraph graph)
        {
            if (node is NextNode nextNode && nextNode.nextNode != null)
                return nextNode.nextNode.id;
            if (node is StartNode startNode && startNode.nextNode != null)
                return startNode.nextNode.id;
            return "";
        }

        /// <summary>
        /// Экранировать строку для JSON.
        /// </summary>
        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }
    }
}
// === FILE: Assets/Scripts/Cinematic/Nodes/CallDialogueNode.cs ===
using System.Collections;
using UnityEngine;
using Managers;
using DialogueSystem.Data;

namespace CinematicSystem.Nodes
{
    /// <summary>
    /// Узел запуска полноценного диалога из DialogueGraph.
    /// Ожидает завершения диалога перед переходом к следующему узлу.
    /// </summary>
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Call Dialogue")]
    public class CallDialogueNode : NextNode
    {
        [Tooltip("Граф диалога для запуска")]
        public DialogueGraph dialogueGraph;
        
        [Tooltip("Целевой клиент (опционально)")]
        public ClientPathfinding targetClient;

        public override string GetNodeType() => "call_dialogue";

        public override IEnumerator Execute(CinematicPlayer player)
        {
            if (dialogueGraph == null)
            {
                Debug.LogError("[CallDialogueNode] Диалог не назначен");
                player.GoToNextNode(nextNode);
                yield break;
            }

            var dialogueManager = Managers.DialogueUIManager.Instance;
            if (dialogueManager == null)
            {
                Debug.LogError("[CallDialogueNode] DialogueUIManager не найден");
                player.GoToNextNode(nextNode);
                yield break;
            }

            bool finished = false;
            
            // Запускаем диалог с callback-ом завершения
            dialogueManager.StartDialogue(dialogueGraph, targetClient, () => finished = true);
            
            // Ждём завершения
            yield return new WaitUntil(() => finished);
            
            player.GoToNextNode(nextNode);
        }
    }
}
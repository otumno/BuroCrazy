// === FILE: Assets/Scripts/Cinematic/Nodes/SayDialogNode.cs ===
using System.Collections;
using UnityEngine;
using Characters;
using DialogueSystem.Data;
using Managers;

namespace CinematicSystem.Nodes
{
    /// <summary>
    /// Узел отображения простого диалога с портретом.
    /// Если задан <see cref="dialogueGraph"/>, используется полноценный DialogueUIManager.
    /// Иначе показывает текст через ThoughtBubbleController (фолбэк).
    /// </summary>
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Say Dialog")]
    public class SayDialogNode : NextNode
    {
        [Tooltip("Полноценный граф диалога (опционально). Если задан — используется DialogueUIManager.")]
        public DialogueGraph dialogueGraph;
        
        [TextArea(3, 10)]
        public string text;
        
        public string speakerName = "Директор";
        
        public Sprite portrait;
        
        public AudioClip voiceClip;
        
        [Tooltip("Продолжительность (0 = ждать клика)")]
        public float duration = 0f;

        public override string GetNodeType() => "say_dialog";

        public override IEnumerator Execute(CinematicPlayer player)
        {
            // Если назначен полноценный граф диалога — используем DialogueUIManager
            if (dialogueGraph != null)
            {
                var dialogueManager = DialogueUIManager.Instance;
                if (dialogueManager != null)
                {
                    Debug.Log($"[SayDialogNode] Запуск диалога: {dialogueGraph.name}");
                    bool finished = false;
                    dialogueManager.StartDialogue(dialogueGraph, null, () => finished = true);
                    yield return new WaitUntil(() => finished);
                    player.GoToNextNode(nextNode);
                    yield break;
                }
                else
                {
                    Debug.LogWarning("[SayDialogNode] DialogueUIManager не найден, использую фолбэк");
                }
            }
            
            // Показываем в консоли для отладки
            Debug.Log($"[Диалог] {speakerName}: {text}");
            
            // Используем ThoughtBubble директора для показа
            var director = DirectorAvatarController.Instance;
            if (director != null)
            {
                var bubble = director.GetComponent<ThoughtBubbleController>();
                bubble?.ShowPriorityMessage(text, duration > 0 ? duration : 3f, Color.white);
            }

            if (duration > 0)
            {
                yield return new WaitForSecondsRealtime(duration);
            }
            else
            {
                // Ждём клика
                while (!Input.GetMouseButtonDown(0))
                    yield return null;
                yield return null;
            }

            player.GoToNextNode(nextNode);
        }
    }
}

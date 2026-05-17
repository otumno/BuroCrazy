// === FILE: Assets/Scripts/Cinematic/Nodes/SayDialogNode.cs ===
using System.Collections;
using UnityEngine;
using Characters;

namespace CinematicSystem.Nodes
{
    /// <summary>
    /// Узел отображения простого диалога с портретом.
    /// Использует ThoughtBubbleController для показа (временно).
    /// В будущем - DialogueUIManager.ShowSimpleMessage.
    /// </summary>
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Say Dialog")]
    public class SayDialogNode : NextNode
    {
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
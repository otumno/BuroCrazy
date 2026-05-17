// === FILE: Assets/Scripts/Cinematic/Nodes/SayBubbleNode.cs ===
using System.Collections;
using UnityEngine;
using Characters;

namespace CinematicSystem.Nodes
{
    /// <summary>
    /// Узел отображения реплики в облачке мысли.
    /// Если duration = 0, ждёт клика мыши для продолжения.
    /// </summary>
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Say Bubble")]
    public class SayBubbleNode : NextNode
    {
        [TextArea(3, 10)]
        public string text;
        
        public string speakerID = "Director";
        
        [Tooltip("Продолжительность (0 = ждать клика)")]
        public float duration = 0f;
        
        public AudioClip voiceClip;

        public override string GetNodeType() => "say_bubble";

        public override IEnumerator Execute(CinematicPlayer player)
        {
            var character = CharacterRegistry.Instance.GetCharacter(speakerID);
            if (character == null)
            {
                Debug.LogError($"[SayBubbleNode] Персонаж не найден: {speakerID}");
                player.GoToNextNode(nextNode);
                yield break;
            }

            var bubble = character.GetComponent<ThoughtBubbleController>();
            if (bubble == null)
            {
                Debug.LogError($"[SayBubbleNode] У персонажа {speakerID} нет ThoughtBubbleController");
                player.GoToNextNode(nextNode);
                yield break;
            }

            // Показываем реплику
            bubble.ShowPriorityMessage(text, duration > 0 ? duration : 0f, Color.white);
            
            // Если duration == 0, ждём клика мыши
            if (duration <= 0)
            {
                // Блокируем выполнение, пока не будет нажата левая кнопка мыши
                while (!Input.GetMouseButtonDown(0))
                    yield return null;
                // Небольшая задержка, чтобы не перескочить следующий узел мгновенно
                yield return null;
            }
            else
            {
                // Ждём указанное время
                if (duration > 0)
                    yield return new WaitForSecondsRealtime(duration);
            }

            player.GoToNextNode(nextNode);
        }
    }
}
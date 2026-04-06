using UnityEngine;

namespace DialogueSystem.Data
{
    [CreateAssetMenu(menuName = "Bureau/Dialogue/Nodes/End Node")]
    public class EndNode : DialogueNode
    {
        public enum DialogueOutcome
        {
            LeaveHappy,
            LeaveAngry,
            LeaveUpset,
            GoToCashier,
            BecomeConfused
        }

        [Header("Звук завершения")]
        public AudioClip endSound;

        [Header("Последствия диалога")]
        [Tooltip("Что сделает клиент после закрытия диалога?")]
        public DialogueOutcome outcome = DialogueOutcome.LeaveHappy;

        [Tooltip("Какую эмоцию установить клиенту?")]
        public Emotion outputEmotion = Emotion.Neutral;

        [Tooltip("Изменение стресса (положительное число = больше стресса)")]
        public float stressModifier = 0f;

        public override NodeType GetNodeType() => NodeType.End;
    }
}
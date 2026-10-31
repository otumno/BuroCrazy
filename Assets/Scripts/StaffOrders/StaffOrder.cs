using UnityEngine;

namespace StaffOrders
{
    /// <summary>
    /// Приказ директора конкретному сотруднику: действие с приоритетом, который со временем падает до нуля.
    /// Пока приоритет высокий, сотрудник бросает всё ради приказа; когда он упадёт, сотрудник снова решает сам.
    /// </summary>
    public class StaffOrder
    {
        public const float DefaultInitialPriority = 10000f;
        public const float DefaultDecaySeconds = 60f;
        public const float DefaultStressCost = 5f;

        public StaffAction Action { get; }
        public float InitialPriority { get; }
        public float DecaySeconds { get; }
        public float StressCost { get; }

        private float _startTime;

        public StaffOrder(
            StaffAction action,
            float initialPriority = DefaultInitialPriority,
            float decaySeconds = DefaultDecaySeconds,
            float stressCost = DefaultStressCost)
        {
            Action = action;
            InitialPriority = initialPriority;
            DecaySeconds = decaySeconds;
            StressCost = stressCost;
        }

        public void Start(float time) => _startTime = time;

        // Линейное падение, а не экспонента: его проще настраивать одним числом (секунды до нуля).
        public float GetPriorityBonus(float time)
        {
            if (DecaySeconds <= 0f) return 0f;

            float progress = Mathf.Clamp01((time - _startTime) / DecaySeconds);
            return InitialPriority * (1f - progress);
        }

        public bool IsExpired(float time) => GetPriorityBonus(time) <= 0f;
    }
}

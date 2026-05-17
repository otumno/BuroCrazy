// === FILE: Assets/Scripts/Cinematic/Nodes/RandomNode.cs ===
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CinematicSystem.Nodes
{
    /// <summary>
    /// Взвешенный исход для случайного выбора.
    /// </summary>
    [System.Serializable]
    public class RandomOutcome
    {
        [Tooltip("Вес исхода (0-1). Чем больше, тем вероятнее выбор.")]
        [Range(0f, 1f)]
        public float weight = 1f;
        
        [Tooltip("Следующий узел при выборе этого исхода")]
        public CinematicNode nextNode;
    }

    /// <summary>
    /// Узел случайного выбора.
    /// Выбирает один из исходов на основе весов (0-1).
    /// </summary>
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Random")]
    public class RandomNode : CinematicNode
    {
        [Tooltip("Список возможных исходов с весами (0-1)")]
        public List<RandomOutcome> outcomes = new List<RandomOutcome>();

        public override string GetNodeType() => "random";

        private void Reset()
        {
            // Создаём 2 исхода по умолчанию при создании через Create Asset
            outcomes = new List<RandomOutcome>
            {
                new RandomOutcome { weight = 1f },
                new RandomOutcome { weight = 1f }
            };
        }

        public override IEnumerator Execute(CinematicPlayer player)
        {
            if (outcomes == null || outcomes.Count == 0)
            {
                Debug.LogWarning("[RandomNode] Нет исходов для выбора");
                yield break;
            }

            // Вычисляем сумму весов (веса уже в диапазоне 0-1)
            float totalWeight = 0f;
            foreach (var outcome in outcomes)
            {
                if (outcome.nextNode != null)
                    totalWeight += outcome.weight;
            }

            if (totalWeight <= 0f)
            {
                Debug.LogWarning("[RandomNode] Сумма весов = 0, выбираем первый исход");
                player.GoToNextNode(outcomes[0].nextNode);
                yield break;
            }

            // Случайный выбор с учётом весов (нормализуем веса)
            float randomValue = Random.Range(0f, totalWeight);
            float currentWeight = 0f;
            CinematicNode selectedNode = outcomes.Count > 0 ? outcomes[0].nextNode : null;

            foreach (var outcome in outcomes)
            {
                if (outcome.nextNode == null) continue;
                
                currentWeight += outcome.weight;
                if (randomValue <= currentWeight)
                {
                    selectedNode = outcome.nextNode;
                    break;
                }
            }

            Debug.Log($"[RandomNode] Выбран исход: {selectedNode?.name ?? "null"} (random={randomValue:F2}/{totalWeight:F2})");
            player.GoToNextNode(selectedNode);
            yield break;
        }
    }
}
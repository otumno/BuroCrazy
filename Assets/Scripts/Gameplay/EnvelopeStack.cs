// Assets/Scripts/Gameplay/EnvelopeStack.cs
using UnityEngine;

namespace Gameplay
{
    public class EnvelopeStack : MonoBehaviour
    {
        // ИСПРАВЛЕНИЕ: Добавляем maxCapacity
        public int maxCapacity = 20;
        public const int PreparationCost = 100;
        
        public int CurrentEnvelopeCount { get; private set; } = 0;

        public void RestoreCount(int count) => CurrentEnvelopeCount = Mathf.Clamp(count, 0, Mathf.Max(0, maxCapacity));

        public void AddEnvelope()
        {
            if (CurrentEnvelopeCount < maxCapacity)
            {
                CurrentEnvelopeCount++;
                // Визуализация...
            }
        }

        public bool TryPrepareEnvelope(Managers.PlayerWallet wallet, string description)
        {
            if (wallet == null || !wallet.CanAfford(PreparationCost) || CurrentEnvelopeCount >= maxCapacity)
                return false;
            AddEnvelope();
            wallet.AddMoney(-PreparationCost, description);
            return true;
        }
        
        public void TakeOneEnvelope() // Если нужен
        {
             if (CurrentEnvelopeCount > 0) CurrentEnvelopeCount--;
        }
    }
}

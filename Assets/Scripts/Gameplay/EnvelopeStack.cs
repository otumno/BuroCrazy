// Assets/Scripts/Gameplay/EnvelopeStack.cs
using UnityEngine;

namespace Gameplay
{
    public class EnvelopeStack : MonoBehaviour
    {
        // ИСПРАВЛЕНИЕ: Добавляем maxCapacity
        public int maxCapacity = 20;
        
        public int CurrentEnvelopeCount { get; private set; } = 0;

        public void AddEnvelope()
        {
            if (CurrentEnvelopeCount < maxCapacity)
            {
                CurrentEnvelopeCount++;
                // Визуализация...
            }
        }
        
        public void TakeOneEnvelope() // Если нужен
        {
             if (CurrentEnvelopeCount > 0) CurrentEnvelopeCount--;
        }
    }
}
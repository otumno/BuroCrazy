// Assets/Scripts/Gameplay/ResourceContainer.cs
using UnityEngine;
using System.Collections.Generic;

namespace Gameplay
{
    public class ResourceContainer : MonoBehaviour
    {
        [Header("Настройки")]
        public ResourceType resourceType;
        public int maxAmount = 10;
        public int currentAmount;
        [Tooltip("При каком количестве звать менеджера")]
        public int criticalThreshold = 3;

        [Header("Визуализация")]
        [Tooltip("Спрайты от пустого (0) до полного (последний).")]
        public List<Sprite> fillSprites;
        public SpriteRenderer targetRenderer;

        private void Start()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
            UpdateVisuals();
        }

        public bool IsEmpty => currentAmount <= 0;
        public bool NeedsReplenishment => currentAmount <= criticalThreshold;

        public bool Consume()
        {
            if (currentAmount > 0)
            {
                currentAmount--;
                UpdateVisuals();
                return true;
            }
            return false;
        }

        public void Replenish()
        {
            currentAmount = maxAmount;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (targetRenderer == null || fillSprites == null || fillSprites.Count == 0) return;

            float ratio = (float)currentAmount / maxAmount;
            int index = Mathf.FloorToInt(ratio * (fillSprites.Count - 1));
            index = Mathf.Clamp(index, 0, fillSprites.Count - 1);

            targetRenderer.sprite = fillSprites[index];
        }
    }
}
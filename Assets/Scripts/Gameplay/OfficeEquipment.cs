using UnityEngine;
using Enums;
using Managers;

namespace Gameplay
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class OfficeEquipment : MonoBehaviour
    {
        [Header("Тип оборудования")]
        public EquipmentType equipmentType;

        [Header("Состояние")]
        [Tooltip("Работоспособно ли оборудование")]
        public bool isOperational = true;

        [Tooltip("Прочность оборудования (0-100)")]
        [Range(0f, 100f)]
        public float durability = 100f;

        [Tooltip("Скорость работы с этим оборудованием (множитель)")]
        [Range(0.1f, 2f)]
        public float speedMultiplier = 1f;

        [Header("Ресурсы (для кулера, сейфа и т.д.)")]
        public bool hasInfiniteResources = true;
        public float currentResources = 0f;
        public float maxResources = 100f;

        [Header("Визуал")]
        public Sprite operationalSprite;
        public Sprite brokenSprite;

        private SpriteRenderer spriteRenderer;
        private OfficeObjectDurability durabilityComponent;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            durabilityComponent = GetComponent<OfficeObjectDurability>();
        }

        private void Start()
        {
            UpdateVisuals();

            if (EquipmentManager.Instance != null)
            {
                EquipmentManager.Instance.RegisterEquipment(this);
            }
        }

        private void OnDestroy()
        {
            if (EquipmentManager.Instance != null)
            {
                EquipmentManager.Instance.UnregisterEquipment(this);
            }
        }

        public bool IsWorking()
        {
            bool durabilityOk = durabilityComponent == null || durabilityComponent.CurrentState != OfficeObjectDurability.State.Broken;
            return isOperational && durabilityOk;
        }

        public void UseEquipment(float amount = 1f)
        {
            if (!hasInfiniteResources)
            {
                currentResources = Mathf.Max(0, currentResources - amount);
                UpdateResourceVisuals();
            }

            if (durabilityComponent != null)
            {
                durabilityComponent.Degrade(amount * 0.01f);
            }
        }

        public void RepairEquipment()
        {
            durability = 100f;
            isOperational = true;

            if (durabilityComponent != null)
            {
                durabilityComponent.Repair();
            }

            UpdateVisuals();
        }

        public void SetOperational(bool operational)
        {
            isOperational = operational;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (spriteRenderer == null) return;

            if (!isOperational && brokenSprite != null)
            {
                spriteRenderer.sprite = brokenSprite;
            }
            else if (operationalSprite != null)
            {
                spriteRenderer.sprite = operationalSprite;
            }

            spriteRenderer.enabled = isOperational;
        }

        private void UpdateResourceVisuals()
        {
            // Здесь можно добавить визуализацию ресурсов (например, уровень воды в кулере)
        }

        public float GetEfficiency()
        {
            if (!IsWorking()) return 0f;

            float durabilityEfficiency = durabilityComponent != null ?
                durabilityComponent.GetEfficiencyMultiplier() : 1f;

            return durabilityEfficiency * speedMultiplier;
        }
    }
}

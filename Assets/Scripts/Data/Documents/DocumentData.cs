using UnityEngine;
using Enums;
using Characters;

namespace Data.Documents
{
    [CreateAssetMenu(fileName = "DocumentData_New", menuName = "Bureau/Documents/Document Data")]
    public class DocumentData : ScriptableObject
    {
        [Header("Базовые данные")]
        public DocumentType documentType;
        public DocumentSubtype subtype;
        public string displayName;
        [TextArea(2, 4)]
        public string description;

        [Header("Требования к оборудованию")]
        [Tooltip("Оборудование, необходимое для обработки этого документа")]
        public EquipmentType requiredEquipment;
        [Tooltip("Если true, документ можно обрабатывать БЕЗ оборудования, но с штрафом к скорости")]
        public bool canProcessWithoutEquipment = false;
        [Tooltip("Штраф к скорости обработки если нет нужного оборудования (0-1)")]
        [Range(0f, 1f)]
        public float equipmentPenalty = 0.5f;

        [Header("Требования к навыкам")]
        [Tooltip("Минимальный уровень работника для обработки этого документа")]
        public int minSkillLevel = 1;
        [Tooltip("Список разрешенных ролей (если пусто - все роли могут обрабатывать)")]
        public System.Collections.Generic.List<StaffController.Role> allowedRoles;

        [Header("Временные затраты")]
        [Tooltip("Базовое время обработки в секундах")]
        public float baseProcessingTime = 5f;

        [Header("Визуал")]
        public Sprite icon;
        public Color documentColor = Color.white;

        [Header("Стоимость")]
        public int baseFee = 10;
        public bool isFree = false;

        public bool CanBeProcessedBy(StaffController.Role role, int skillLevel, bool hasEquipment)
        {
            if (hasEquipment && requiredEquipment != EquipmentType.None && !canProcessWithoutEquipment)
            {
                if (!hasEquipment) return false;
            }

            if (skillLevel < minSkillLevel) return false;

            if (allowedRoles != null && allowedRoles.Count > 0)
            {
                if (!allowedRoles.Contains(role)) return false;
            }

            return true;
        }

        public float GetProcessingTimeWithPenalty(bool hasEquipment)
        {
            if (requiredEquipment == EquipmentType.None || hasEquipment || canProcessWithoutEquipment)
            {
                return baseProcessingTime;
            }

            return baseProcessingTime * (1f + equipmentPenalty);
        }
    }
}

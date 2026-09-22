// Assets/Scripts/Data/Documents/ProjectDocumentDefinition.cs
using UnityEngine;
using Scriptables.Progression;
using Enums;

namespace Data.Documents
{
    /// <summary>
    /// Описание проектного документа, проходящего по цепочке
    /// (директор → регистратор → касса → архив).
    /// </summary>
    [System.Serializable]
    public class ProjectDocumentDefinition
    {
        [Header("Основное")]
        public string documentName;
        public ProjectDocumentType docType;

        [Header("Цели")]
        public RegionData targetRegion;
        public JobTitleData targetJob;
        public string targetUpgradeID;

        [Header("Флаги состояния")]
        public bool signedByDirector;
        public bool processedByRegistrar;
        public bool paidAtCashier;
        public bool archived;

        [Header("Должность (Job Promotion)")]
        [Tooltip("Каким путём получена должность: None / Correct / Alternate.")]
        public PathType jobPathType = PathType.None;
        [Tooltip("Стоимость в деньгах для alternate пути.")]
        public int jobMoneyCost = 0;
        [Tooltip("Прирост коррупции в процентах для alternate пути.")]
        public int jobCorruptionCost = 0;

        public ProjectDocumentDefinition() { }

        public ProjectDocumentDefinition(RegionData region)
        {
            docType = ProjectDocumentType.RegionUnlock;
            targetRegion = region;
            documentName = region != null ? $"Приказ о реновации: {region.displayName}" : "Реновация";
        }

        public ProjectDocumentDefinition(JobTitleData job)
        {
            docType = ProjectDocumentType.JobPromotion;
            targetJob = job;
            documentName = job != null ? $"Приказ о назначении: {job.titleName}" : "Назначение";
        }

        public ProjectDocumentDefinition(string upgradeID, string upgradeName)
        {
            docType = ProjectDocumentType.FacilityUpgrade;
            targetUpgradeID = upgradeID;
            documentName = $"Закупка: {upgradeName}";
        }

        public ProjectDocumentDefinition(string policyID, string policyTitle, bool isPolicy)
        {
            docType = ProjectDocumentType.Policy;
            targetUpgradeID = policyID;
            documentName = policyTitle;
            signedByDirector = false;
            processedByRegistrar = true;
            paidAtCashier = true;
            archived = false;
        }

        /// <summary>
        /// Тип пути для документа о повышении.
        /// </summary>
        public enum PathType
        {
            None = 0,
            Correct = 1,
            Alternate = 2
        }
    }
}

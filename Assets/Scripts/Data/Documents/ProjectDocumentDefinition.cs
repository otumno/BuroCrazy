// Assets/Scripts/Data/Documents/ProjectDocumentDefinition.cs
using UnityEngine;
using Scriptables.Progression;

namespace Data.Documents
{
    public enum ProjectDocumentType
    {
        RegionUnlock,   // Приказ о захвате региона
        JobPromotion,   // Приказ о повышении директора
        FacilityUpgrade // Приказ о закупке (апгрейд)
    }

    /// <summary>
    /// Этот класс описывает содержание важного документа (не клиентского).
    /// Экземпляр этого класса будет жить внутри физической папки на столе.
    /// </summary>
    [System.Serializable]
    public class ProjectDocumentDefinition
    {
        public string documentName; // Название для UI ("Приказ №66")
        public ProjectDocumentType docType;
        
        // Ссылки на данные (заполняется только одно поле в зависимости от типа)
        public RegionData targetRegion;
        public JobTitleData targetJob;
        public string targetUpgradeID; // ID апгрейда из UpgradeManager

        // Статус прохождения инстанций (для визуализации печатей на документе)
        public bool signedByDirector;
        public bool processedByRegistrar;
        public bool paidAtCashier;
        public bool archived; // Финальная стадия

        // Конструктор для Региона
        public ProjectDocumentDefinition(RegionData region)
        {
            docType = ProjectDocumentType.RegionUnlock;
            targetRegion = region;
            documentName = $"Приказ о реновации: {region.displayName}";
        }

        // Конструктор для Должности
        public ProjectDocumentDefinition(JobTitleData job)
        {
            docType = ProjectDocumentType.JobPromotion;
            targetJob = job;
            documentName = $"Приказ о назначении: {job.titleName}";
        }
        
        // Конструктор для Апгрейда
        public ProjectDocumentDefinition(string upgradeID, string upgradeName)
        {
            docType = ProjectDocumentType.FacilityUpgrade;
            targetUpgradeID = upgradeID;
            documentName = $"Закупка: {upgradeName}";
        }
    }
}
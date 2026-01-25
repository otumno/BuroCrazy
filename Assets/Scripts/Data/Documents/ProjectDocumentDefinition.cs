// Assets/Scripts/Data/Documents/ProjectDocumentDefinition.cs
using UnityEngine;
using Scriptables.Progression;
using Enums;

namespace Data.Documents
{
    [System.Serializable]
    public class ProjectDocumentDefinition
    {
        public string documentName; 
        public ProjectDocumentType docType; // Ссылка на глобальный Enum
        
        public RegionData targetRegion;
        public JobTitleData targetJob;
        public string targetUpgradeID; 

        // --- ФЛАГИ СОСТОЯНИЯ ---
        public bool signedByDirector;      
        public bool processedByRegistrar;  
        public bool paidAtCashier;          
        public bool archived;               

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
    }
}
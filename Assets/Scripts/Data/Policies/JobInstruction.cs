using System.Collections.Generic;
using UnityEngine;
using Characters;

namespace Data.Policies
{
    [CreateAssetMenu(fileName = "JobInstruction", menuName = "Bureau/Policies/Job Instruction")]
    public class JobInstruction : ScriptableObject
    {
        [Header("Идентификация")]
        public string instructionID;
        public string displayName;

        [Header("Категория")]
        public InstructionCategory category;
        public bool isDefaultEnabled = false;

        [Header("Описание")]
        [TextArea(2, 4)]
        public string description;
        [TextArea(2, 4)]
        public string effectDescription;

        [Header("Влияние на геймплей")]
        public float workSpeedModifier = 1f;
        public float stressModifier = 1f;
        public float incomeModifier = 1f;

        [Header("Требования")]
        public List<string> requiredUpgrades = new List<string>();
        public List<string> requiredRegionIDs = new List<string>();

        [Header("Роли")]
        public List<StaffController.Role> applicableRoles = new List<StaffController.Role>
        {
            StaffController.Role.Intern,
            StaffController.Role.Registrar,
            StaffController.Role.Cashier,
            StaffController.Role.Clerk
        };

        public enum InstructionCategory
        {
            Greeting,
            Priority,
            SpecialClients,
            MoneyHandling,
            Breaks,
            DocumentProcessing
        }

        public bool CanBeEnabled()
        {
            if (Managers.UpgradeManager.Instance == null) return false;

            foreach (var upgradeID in requiredUpgrades)
            {
                var upgrade = Managers.UpgradeManager.Instance.allUpgradesDatabase
                    .Find(u => u != null && u.name == upgradeID);

                if (upgrade == null || !Managers.UpgradeManager.Instance.IsUpgradePurchased(upgrade))
                {
                    return false;
                }
            }

            if (Managers.ProgressionManager.Instance != null)
            {
                foreach (var regionID in requiredRegionIDs)
                {
                    if (!Managers.ProgressionManager.Instance.IsRegionUnlocked(regionID))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}

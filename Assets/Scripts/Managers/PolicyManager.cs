// Assets/Scripts/Managers/PolicyManager.cs
using UnityEngine;
using System.Collections.Generic;
using Data.Policies;
using System.Linq;
using Data.Documents; // Для ProjectDocumentDefinition
// using Characters.Controllers; // <--- УДАЛИЛ ЭТУ СТРОКУ, ОНА ВЫЗЫВАЛА ОШИБКУ

namespace Managers
{
    public class PolicyManager : MonoBehaviour
    {
        public static PolicyManager Instance { get; private set; }

        [Header("База данных")]
        public List<PolicyData> allPoliciesDatabase; 
        public GameObject policyDocumentPrefab;      

        [Header("Состояние")]
        public HashSet<string> activePolicyIDs = new HashSet<string>();
        
        public float GlobalWorkSpeedMod { get; private set; } = 1.0f;
        public float GlobalStressMod { get; private set; } = 1.0f;
        public float GlobalIncomeMod { get; private set; } = 1.0f;

        void Awake()
        {
            if (Instance != null) Destroy(gameObject);
            else Instance = this;
        }

        public void DraftPolicy(string policyID)
        {
            if (activePolicyIDs.Contains(policyID)) return; 

            var policy = allPoliciesDatabase.FirstOrDefault(p => p.id == policyID);
            if (policy == null) 
            {
                Debug.LogWarning($"[PolicyManager] Политика с ID {policyID} не найдена в базе!");
                return;
            }

            var directorStack = FindDirectorStack();

            if (directorStack != null)
            {
                // Используем конструктор для Политик
                ProjectDocumentDefinition docDef = new ProjectDocumentDefinition(policy.id, policy.documentTitle, true);

                directorStack.AddProjectDocument(docDef, policyDocumentPrefab);
                Debug.Log($"[PolicyManager] Проект указа '{policy.displayName}' положен на стол Директора.");
            }
            else
            {
                Debug.LogError("[PolicyManager] Не могу найти стол Директора!");
            }
        }

        public void ActivatePolicy(string policyID)
        {
            if (!activePolicyIDs.Contains(policyID))
            {
                activePolicyIDs.Add(policyID);
                RecalculateModifiers();
                Debug.Log($"[PolicyManager] Указ {policyID} вступил в силу!");
            }
        }

        public void DeactivatePolicy(string policyID)
        {
            if (activePolicyIDs.Contains(policyID))
            {
                activePolicyIDs.Remove(policyID);
                RecalculateModifiers();
                Debug.Log($"[PolicyManager] Указ {policyID} отменен.");
            }
        }

        private void RecalculateModifiers()
        {
            GlobalWorkSpeedMod = 1.0f;
            GlobalStressMod = 1.0f;
            GlobalIncomeMod = 1.0f;

            foreach (var pid in activePolicyIDs)
            {
                var p = allPoliciesDatabase.FirstOrDefault(x => x.id == pid);
                if (p != null)
                {
                    GlobalWorkSpeedMod *= p.workSpeedMultiplier;
                    GlobalStressMod *= p.stressGrowthMultiplier;
                    GlobalIncomeMod *= p.incomeMultiplier;
                }
            }
        }

        private DocumentStack FindDirectorStack()
        {
            // DirectorAvatarController скорее всего в глобальном namespace, поэтому просто ищем его
            // Если он не находится, попробуйте добавить "global::" перед именем класса, но обычно это не нужно
            var director = FindFirstObjectByType<DirectorAvatarController>();
            
            if (director != null && director.assignedWorkstation != null)
            {
                return director.assignedWorkstation.documentStack;
            }
            
            return null;
        }
		
		public Data.Policies.PolicyData GetPolicyById(string id)
			{
				return allPoliciesDatabase.FirstOrDefault(p => p.id == id);
			}
    }
}
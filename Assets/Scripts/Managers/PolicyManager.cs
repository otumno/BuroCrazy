using UnityEngine;
using System.Collections.Generic;
using Data.Policies;
using System.Linq;
using Data.Documents;

namespace Managers
{
    public class PolicyManager : MonoBehaviour
    {
        public static PolicyManager Instance { get; private set; }

        [Header("База данных")]
        public List<PolicyData> allPoliciesDatabase; 
        public GameObject policyDocumentPrefab;      

        [Header("Связи со сценой")]
        [Tooltip("Перетащите сюда стопку входящих документов Директора (DirectorInboxStack)")]
        public DocumentStack directorInboxStack;

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

            if (directorInboxStack != null)
            {
                // Используем конструктор для Политик
                ProjectDocumentDefinition docDef = new ProjectDocumentDefinition(policy.id, policy.documentTitle, true);

                directorInboxStack.AddProjectDocument(docDef, policyDocumentPrefab);
                Debug.Log($"[PolicyManager] Проект указа '{policy.displayName}' положен на стол Директора.");
            }
            else
            {
                Debug.LogError("[PolicyManager] Не могу найти стол Директора! Поле Director Inbox Stack пустое в инспекторе.");
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
		
        public Data.Policies.PolicyData GetPolicyById(string id)
        {
            return allPoliciesDatabase.FirstOrDefault(p => p.id == id);
        }

        public bool IsBehaviorActive(string flag, StaffController.Role role)
        {
            if (string.IsNullOrEmpty(flag)) return false;

            foreach (var pid in activePolicyIDs)
            {
                var p = GetPolicyById(pid);
                if (p != null && p.behaviorFlag == flag)
                {
                    if (p.applicableRoles == null || p.applicableRoles.Count == 0 || p.applicableRoles.Contains(role))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}

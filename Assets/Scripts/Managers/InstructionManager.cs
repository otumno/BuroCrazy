using System.Collections.Generic;
using UnityEngine;
using Characters;

namespace Managers
{
    public class InstructionManager : MonoBehaviour
    {
        public static InstructionManager Instance { get; private set; }

        [Header("База инструкций")]
        public JobInstructionDatabase instructionDatabase;

        [Header("Активные инструкции (Runtime)")]
        private HashSet<string> enabledInstructionIDs = new HashSet<string>();

        [Header("Настройки по умолчанию")]
        public List<string> defaultEnabledInstructions = new List<string>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Применяем инструкции по умолчанию
            foreach (var id in defaultEnabledInstructions)
            {
                enabledInstructionIDs.Add(id);
            }
        }

        public void EnableInstruction(string instructionID)
        {
            enabledInstructionIDs.Add(instructionID);
            Debug.Log($"[InstructionManager] Инструкция включена: {instructionID}");
        }

        public void DisableInstruction(string instructionID)
        {
            enabledInstructionIDs.Remove(instructionID);
            Debug.Log($"[InstructionManager] Инструкция выключена: {instructionID}");
        }

        public bool IsInstructionEnabled(string instructionID)
        {
            return enabledInstructionIDs.Contains(instructionID);
        }

        public bool IsInstructionEnabled(Data.Policies.JobInstruction instruction)
        {
            if (instruction == null) return false;
            return enabledInstructionIDs.Contains(instruction.instructionID);
        }

        public List<Data.Policies.JobInstruction> GetAllEnabledInstructions()
        {
            var result = new List<Data.Policies.JobInstruction>();

            if (instructionDatabase == null || instructionDatabase.allInstructions == null)
                return result;

            foreach (var instruction in instructionDatabase.allInstructions)
            {
                if (instruction != null && enabledInstructionIDs.Contains(instruction.instructionID))
                {
                    result.Add(instruction);
                }
            }

            return result;
        }

        public List<Data.Policies.JobInstruction> GetEnabledInstructionsForRole(StaffController.Role role)
        {
            var result = new List<Data.Policies.JobInstruction>();

            if (instructionDatabase == null || instructionDatabase.allInstructions == null)
                return result;

            foreach (var instruction in instructionDatabase.allInstructions)
            {
                if (instruction != null &&
                    enabledInstructionIDs.Contains(instruction.instructionID) &&
                    instruction.applicableRoles.Contains(role))
                {
                    result.Add(instruction);
                }
            }

            return result;
        }

        public float GetWorkSpeedModifierForRole(StaffController.Role role)
        {
            float modifier = 1f;

            foreach (var instruction in GetEnabledInstructionsForRole(role))
            {
                modifier *= instruction.workSpeedModifier;
            }

            return modifier;
        }

        public float GetStressModifierForRole(StaffController.Role role)
        {
            float modifier = 1f;

            foreach (var instruction in GetEnabledInstructionsForRole(role))
            {
                modifier *= instruction.stressModifier;
            }

            return modifier;
        }

        public void ResetInstructions()
        {
            enabledInstructionIDs.Clear();
            foreach (var id in defaultEnabledInstructions)
            {
                enabledInstructionIDs.Add(id);
            }
            Debug.Log("[InstructionManager] Инструкции сброшены к умолчанию");
        }

        public List<string> GetEnabledInstructionIDs()
        {
            return new List<string>(enabledInstructionIDs);
        }

        public void SetEnabledInstructions(List<string> ids)
        {
            enabledInstructionIDs.Clear();
            foreach (var id in ids)
            {
                enabledInstructionIDs.Add(id);
            }
        }
    }
}

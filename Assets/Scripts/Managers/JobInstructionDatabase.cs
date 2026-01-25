using System.Collections.Generic;
using UnityEngine;
using Characters;

namespace Managers
{
    [CreateAssetMenu(fileName = "JobInstructionDatabase", menuName = "Bureau/Databases/Instruction Database")]
    public class JobInstructionDatabase : ScriptableObject
    {
        public List<Data.Policies.JobInstruction> allInstructions;

        public List<Data.Policies.JobInstruction> GetEnabledInstructions()
        {
            var result = new List<Data.Policies.JobInstruction>();

            if (allInstructions == null) return result;

            foreach (var instruction in allInstructions)
            {
                if (instruction != null && InstructionManager.Instance != null &&
                    InstructionManager.Instance.IsInstructionEnabled(instruction))
                {
                    result.Add(instruction);
                }
            }

            return result;
        }

        public List<Data.Policies.JobInstruction> GetInstructionsForRole(StaffController.Role role)
        {
            var result = new List<Data.Policies.JobInstruction>();

            if (allInstructions == null) return result;

            foreach (var instruction in allInstructions)
            {
                if (instruction != null &&
                    instruction.applicableRoles.Contains(role) &&
                    InstructionManager.Instance.IsInstructionEnabled(instruction))
                {
                    result.Add(instruction);
                }
            }

            return result;
        }

        public Data.Policies.JobInstruction GetInstructionByID(string id)
        {
            if (allInstructions == null) return null;

            foreach (var instruction in allInstructions)
            {
                if (instruction != null && instruction.instructionID == id)
                {
                    return instruction;
                }
            }
            return null;
        }
    }
}

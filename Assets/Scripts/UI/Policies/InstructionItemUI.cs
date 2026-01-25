using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace UI.Policies
{
    public class InstructionItemUI : MonoBehaviour
    {
        [Header("UI элементы")]
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI descriptionText;
        public Toggle enableToggle;
        public Image categoryIcon;

        private Data.Policies.JobInstruction instruction;
        private Managers.InstructionManager instructionManager;

        public void Setup(Data.Policies.JobInstruction instr, Managers.InstructionManager manager)
        {
            instruction = instr;
            instructionManager = manager;

            if (nameText != null)
            {
                nameText.text = instruction.displayName;
            }

            if (descriptionText != null)
            {
                descriptionText.text = instruction.description;
            }

            if (enableToggle != null)
            {
                enableToggle.isOn = manager.IsInstructionEnabled(instruction);
                enableToggle.onValueChanged.AddListener(OnToggleChanged);
            }
        }

        private void OnToggleChanged(bool isOn)
        {
            if (instruction == null || instructionManager == null) return;

            if (isOn)
            {
                if (instruction.CanBeEnabled())
                {
                    instructionManager.EnableInstruction(instruction.instructionID);
                }
                else
                {
                    enableToggle.isOn = false;
                    Debug.Log($"[InstructionItemUI] Невозможно включить инструкцию {instruction.displayName}: требования не выполнены");
                }
            }
            else
            {
                instructionManager.DisableInstruction(instruction.instructionID);
            }
        }
    }
}

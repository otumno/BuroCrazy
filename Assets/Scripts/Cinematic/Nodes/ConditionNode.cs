// === FILE: Assets/Scripts/Cinematic/Nodes/ConditionNode.cs ===
using System.Collections;
using UnityEngine;
using Managers;

namespace CinematicSystem.Nodes
{
    /// <summary>
    /// Узел условного перехода.
    /// Проверяет условие и переходит к trueNode или falseNode.
    /// </summary>
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Condition")]
    public class ConditionNode : CinematicNode
    {
        /// <summary>Узел для истинного условия</summary>
        public CinematicNode trueNode;
        
        /// <summary>Узел для ложного условия</summary>
        public CinematicNode falseNode;
        
        /// <summary>Ключ условия (MONEY, STRIKES, INFLUENCE, или имя флага)</summary>
        public string conditionKey;
        
        /// <summary>Операция сравнения</summary>
        public string operation = "==";
        
        /// <summary>Значение для сравнения</summary>
        public int value;

        public override string GetNodeType() => "condition";

        /// <summary>
        /// Получает текущее значение по ключу условия.
        /// </summary>
        private int GetCurrentValue()
        {
            switch (conditionKey)
            {
                case "MONEY":
                    return PlayerWallet.Instance?.GetCurrentMoney() ?? 0;
                case "STRIKES":
                    return DirectorManager.Instance?.currentStrikes ?? 0;
                case "INFLUENCE":
                    return ProgressionManager.Instance?.GetInfluence() ?? 0;
                case "DAY":
                    return TimeManager.Instance?.GetCurrentDay() ?? 0;
                default:
                    return StoryStateManager.Instance?.GetFlag(conditionKey) ?? 0;
            }
        }

        public override IEnumerator Execute(CinematicPlayer player)
        {
            int current = GetCurrentValue();
            bool result = false;
            
            switch (operation)
            {
                case "==": result = current == value; break;
                case "!=": result = current != value; break;
                case ">":  result = current > value; break;
                case "<":  result = current < value; break;
                case ">=": result = current >= value; break;
                case "<=": result = current <= value; break;
                default:
                    Debug.LogWarning($"[ConditionNode] Неизвестная операция: {operation}");
                    break;
            }

            player.GoToNextNode(result ? trueNode : falseNode);
            yield break;
        }
    }
}
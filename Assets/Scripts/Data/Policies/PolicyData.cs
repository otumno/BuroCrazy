// Assets/Scripts/Data/Policies/PolicyData.cs
using UnityEngine;

namespace Data.Policies
{
    [CreateAssetMenu(fileName = "Policy_New", menuName = "Bureau/Policy Data")]
    public class PolicyData : ScriptableObject
    {
        [Header("Идентификация")]
        public string id;
        public string displayName;
        [TextArea] public string description;

        [Header("Баланс (Trade-offs)")]
        [Tooltip("Множитель скорости работы (1.0 = норма, 1.2 = +20%)")]
        public float workSpeedMultiplier = 1.0f;
        
        [Tooltip("Множитель стресса (1.0 = норма, 0.5 = меньше стресса)")]
        public float stressGrowthMultiplier = 1.0f;

        [Tooltip("Множитель дохода (1.0 = норма)")]
        public float incomeMultiplier = 1.0f;
        
        [Tooltip("Влияние на лояльность клиентов")]
        public float customerSatisfactionBonus = 0f;
        
        [Tooltip("Риск проверки (0-1). 0.1 = +10% к шансу проверки")]
        public float inspectionRiskMod = 0f;

        [Header("Визуал Документа")]
        [Tooltip("Текст, который будет написан на папке при наведении")]
        public string documentTitle = "УКАЗ №...";
    }
}
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

        [Header("Специфичные правила (Для ИИ)")]
        [Tooltip("Роли, к которым применяется это правило (если пусто - ко всем)")]
        public System.Collections.Generic.List<StaffController.Role> applicableRoles = new System.Collections.Generic.List<StaffController.Role>();

        [Tooltip("Специальный флаг для проверки в коде (например, 'PRIORITY_ELDERLY')")]
        public string behaviorFlag = "";

        // ----- Новые поля расширения -----

        [Header("Персонал (Staff)")]
        [Tooltip("Множитель стоимости найма (1.0 = норма, 0.7 = -30%, 1.2 = +20%).")]
        public float hiringCostMultiplier = 1.0f;

        [Tooltip("Множитель скорости появления кандидатов (1.0 = норма, 2.0 = в два раза быстрее).")]
        public float hiringSpeedMultiplier = 1.0f;

        [Tooltip("Множитель скорости потери энергии сотрудников (1.0 = норма, 0.7 = устают медленнее, 1.3 = быстрее).")]
        public float energyLossMultiplier = 1.0f;

        [Tooltip("Множитель скорости потери морали (1.0 = норма, 1.3 = падает быстрее).")]
        public float moraleLossMultiplier = 1.0f;

        [Tooltip("Бонус к максимальному числу сотрудников (0 = без изменений, 1 = +1 сотрудник).")]
        public int maxStaffBonus = 0;

        [Tooltip("Множитель зарплат сотрудников (1.0 = норма, 0.9 = -10%, 1.1 = +10%).")]
        public float salaryMultiplier = 1.0f;

        [Header("Экономика (Economy)")]
        [Tooltip("Множитель стоимости апгрейдов (1.0 = норма, 1.15 = +15%).")]
        public float upgradeCostMultiplier = 1.0f;

        [Tooltip("Множитель штрафов (1.0 = норма, 0.5 = штрафы пополам).")]
        public float fineMultiplier = 1.0f;

        [Tooltip("Добавка к ставке налога (0 = без изменений, 0.1 = +10%).")]
        public float taxRateMod = 0f;

        [Tooltip("Пассивный доход в день (0 = нет, 50 = +50 денег каждый день).")]
        public int passiveIncomePerDay = 0;

        [Header("Клиенты (Clients)")]
        [Tooltip("Множитель шанса скандала (1.0 = норма, 0.6 = реже, 0 = никогда).")]
        public float scandalChanceMod = 1.0f;

        [Tooltip("Множитель времени ожидания клиентов (1.0 = норма, 1.3 = ждут дольше).")]
        public float waitTimeMultiplier = 1.0f;

        [Header("Безопасность (Security)")]
        [Tooltip("Множитель скорости охранников (1.0 = норма, 0.8 = медленнее).")]
        public float guardSpeedMultiplier = 1.0f;

        [Tooltip("Множитель зарплаты охранников (1.0 = норма, 2.0 = вдвое больше).")]
        public float guardSalaryMultiplier = 1.0f;

        [Tooltip("Множитель стресса охранников (1.0 = норма, 1.2 = стрессуют быстрее).")]
        public float guardStressMultiplier = 1.0f;

        [Header("Документы (Documents)")]
        [Tooltip("Множитель скорости регистрации документов (1.0 = норма, 1.4 = быстрее).")]
        public float registrationSpeedMultiplier = 1.0f;

        [Tooltip("Добавка к качеству документов (0 = без изменений, -0.2 = -20% качества).")]
        public float documentQualityMod = 0f;

        [Tooltip("Множитель процента ошибок в документах (1.0 = норма, 0.7 = на 30% меньше ошибок, 1.3 = на 30% больше).")]
        public float errorRateModifier = 1.0f;

        [Header("Коррупция (Corruption)")]
        [Tooltip("Множитель скорости роста коррупции (1.0 = норма, 1.5 = рост быстрее).")]
        public float corruptionGrowthMod = 1.0f;

        [Header("Ночной режим (Night)")]
        [Tooltip("Множитель клиентов ночью (1.0 = норма, 0 = не приходят).")]
        public float nightClientMod = 1.0f;

        [Tooltip("Штраф за простой оборудования ночью (0 = без штрафа, 0.2 = +20% износ).")]
        public float equipmentIdlePenalty = 0f;
    }
}

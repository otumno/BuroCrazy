// Assets/Editor/CreatePolicies.cs
// Редакторский скрипт для пакетного создания / обновления ассетов PolicyData
// (политики / указы) в Resources/Policies/.
//
// Меню:
//   Tools/Bureau/Create Policies              — идемпотентное создание/обновление
//   Tools/Bureau/Create Policies (Force)      — удалить и создать заново
//
// Поведение:
//   1. Ищет существующие PolicyData-ассеты по id через AssetDatabase.FindAssets.
//   2. Если найден — перезаписывает поля актуальными значениями (не дублирует).
//   3. Если не найден — создаёт новый ассет через ScriptableObject.CreateInstance.
//   4. Не трогает ассеты PolicyData, чей id не входит в список ниже.
//
// Шаблон: CreateJobTitles.cs (идемпотентность, EnsureFolder, data-driven Entry).
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Data.Policies;
using UnityEditor;
using UnityEngine;

public static class CreatePolicies
{
    // ----- ID константы политик -----
    public const string Policy_QuickHire         = "Policy_QuickHire";
    public const string Policy_FlexSchedule      = "Policy_FlexSchedule";
    public const string Policy_StaffReserve      = "Policy_StaffReserve";
    public const string Policy_TotalControl      = "Policy_TotalControl";
    public const string Policy_TaxOptimization   = "Policy_TaxOptimization";
    public const string Policy_InvestmentFund    = "Policy_InvestmentFund";
    public const string Policy_HardEconomy       = "Policy_HardEconomy";
    public const string Policy_FinancialImmunity = "Policy_FinancialImmunity";
    public const string Policy_Patrol            = "Policy_Patrol";
    public const string Policy_Curfew            = "Policy_Curfew";
    public const string Policy_ViolationImmunity = "Policy_ViolationImmunity";
    public const string Policy_TotalSecurity     = "Policy_TotalSecurity";
    public const string Policy_Paperwork         = "Policy_Paperwork";
    public const string Policy_FastRegistration  = "Policy_FastRegistration";
    public const string Policy_Bureaucracy       = "Policy_Bureaucracy";
    public const string Policy_RegionalBonus     = "Policy_RegionalBonus";
    public const string Policy_ShadowFunding     = "Policy_ShadowFunding";
    public const string Policy_DebtForgiveness   = "Policy_DebtForgiveness";
    public const string Policy_Forgery           = "Policy_Forgery";
    public const string Policy_MinisterImmunity  = "Policy_MinisterImmunity";

    private const string Folder = "Assets/Resources/Policies";
    private const string LogTag = "[CreatePolicies]";

    [MenuItem("Tools/Bureau/Create Policies")]
    public static void CreateAll() => Run(forceRecreate: false);

    [MenuItem("Tools/Bureau/Create Policies (Force)")]
    public static void CreateAllForce() => Run(forceRecreate: true);

    private static void Run(bool forceRecreate)
    {
        EnsureFolder(Folder);

        var entries = BuildEntries();
        int created = 0;
        int updated = 0;

        foreach (var e in entries)
        {
            string path = $"{Folder}/{e.id}.asset";
            var existing = FindByID(e.id);

            PolicyData asset;
            bool isNew = (existing == null);

            if (forceRecreate && existing != null)
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(existing));
                isNew = true;
            }

            if (isNew)
            {
                asset = ScriptableObject.CreateInstance<PolicyData>();
                AssetDatabase.CreateAsset(asset, path);
                created++;
                Debug.Log($"{LogTag} Создан ассет: {path}");
            }
            else
            {
                asset = existing;
                updated++;
                Debug.Log($"{LogTag} Обновлён ассет: {path}");
            }

            ApplyEntry(asset, e);
            EditorUtility.SetDirty(asset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"{LogTag} Готово. Создано: {created}, обновлено: {updated} (force={forceRecreate}).");
    }

    // ----- Заполнение полей ассета данными из Entry -----
    private static void ApplyEntry(PolicyData asset, Entry e)
    {
        // Старые поля (сохраняются)
        asset.id = e.id;
        asset.displayName = e.displayName;
        asset.description = e.description;
        asset.workSpeedMultiplier = e.workSpeedMultiplier;
        asset.stressGrowthMultiplier = e.stressGrowthMultiplier;
        asset.incomeMultiplier = e.incomeMultiplier;
        asset.customerSatisfactionBonus = e.customerSatisfactionBonus;
        asset.inspectionRiskMod = e.inspectionRiskMod;
        asset.documentTitle = e.documentTitle;
        asset.applicableRoles = e.applicableRoles != null
            ? new List<StaffController.Role>(e.applicableRoles)
            : new List<StaffController.Role>();
        asset.behaviorFlag = e.behaviorFlag;

        // Новые поля — Персонал
        asset.hiringCostMultiplier = e.hiringCostMultiplier;
        asset.hiringSpeedMultiplier = e.hiringSpeedMultiplier;
        asset.energyLossMultiplier = e.energyLossMultiplier;
        asset.moraleLossMultiplier = e.moraleLossMultiplier;
        asset.maxStaffBonus = e.maxStaffBonus;
        asset.salaryMultiplier = e.salaryMultiplier;

        // Новые поля — Экономика
        asset.upgradeCostMultiplier = e.upgradeCostMultiplier;
        asset.fineMultiplier = e.fineMultiplier;
        asset.taxRateMod = e.taxRateMod;
        asset.passiveIncomePerDay = e.passiveIncomePerDay;

        // Новые поля — Клиенты
        asset.scandalChanceMod = e.scandalChanceMod;
        asset.waitTimeMultiplier = e.waitTimeMultiplier;

        // Новые поля — Безопасность
        asset.guardSpeedMultiplier = e.guardSpeedMultiplier;
        asset.guardSalaryMultiplier = e.guardSalaryMultiplier;
        asset.guardStressMultiplier = e.guardStressMultiplier;

        // Новые поля — Документы
        asset.registrationSpeedMultiplier = e.registrationSpeedMultiplier;
        asset.documentQualityMod = e.documentQualityMod;
        asset.errorRateModifier = e.errorRateModifier;

        // Новые поля — Коррупция
        asset.corruptionGrowthMod = e.corruptionGrowthMod;

        // Новые поля — Ночной режим
        asset.nightClientMod = e.nightClientMod;
        asset.equipmentIdlePenalty = e.equipmentIdlePenalty;
    }

    // ----- Поиск существующего ассета по id -----
    private static PolicyData FindByID(string id)
    {
        string[] guids = AssetDatabase.FindAssets("t:PolicyData");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<PolicyData>(path);
            if (asset != null && asset.id == id) return asset;
        }
        return null;
    }

    // ----- Рекурсивное создание папки -----
    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
        string leaf = Path.GetFileName(folder);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    // ----- Описание одной политики -----
    // Все нейтральные значения по умолчанию (1.0 для множителей, 0 для бонусов)
    // подставляются ниже в BuildEntries через object initializer.
    private struct Entry
    {
        // Идентификация
        public string id;
        public string displayName;
        public string description;
        public string documentTitle;
        public List<StaffController.Role> applicableRoles;
        public string behaviorFlag;

        // Старые поля (Баланс)
        public float workSpeedMultiplier;
        public float stressGrowthMultiplier;
        public float incomeMultiplier;
        public float customerSatisfactionBonus;
        public float inspectionRiskMod;

        // Новые — Персонал
        public float hiringCostMultiplier;
        public float hiringSpeedMultiplier;
        public float energyLossMultiplier;
        public float moraleLossMultiplier;
        public int maxStaffBonus;
        public float salaryMultiplier;

        // Новые — Экономика
        public float upgradeCostMultiplier;
        public float fineMultiplier;
        public float taxRateMod;
        public int passiveIncomePerDay;

        // Новые — Клиенты
        public float scandalChanceMod;
        public float waitTimeMultiplier;

        // Новые — Безопасность
        public float guardSpeedMultiplier;
        public float guardSalaryMultiplier;
        public float guardStressMultiplier;

        // Новые — Документы
        public float registrationSpeedMultiplier;
        public float documentQualityMod;
        public float errorRateModifier;

        // Новые — Коррупция
        public float corruptionGrowthMod;

        // Новые — Ночной режим
        public float nightClientMod;
        public float equipmentIdlePenalty;
    }

    // ----- Хелпер для нейтральных значений -----
    private static Entry New(string id, string displayName, string description, string documentTitle)
    {
        return new Entry
        {
            id = id,
            displayName = displayName,
            description = description,
            documentTitle = documentTitle,
            applicableRoles = new List<StaffController.Role>(),
            behaviorFlag = "",

            workSpeedMultiplier = 1.0f,
            stressGrowthMultiplier = 1.0f,
            incomeMultiplier = 1.0f,
            customerSatisfactionBonus = 0f,
            inspectionRiskMod = 0f,

            hiringCostMultiplier = 1.0f,
            hiringSpeedMultiplier = 1.0f,
            energyLossMultiplier = 1.0f,
            moraleLossMultiplier = 1.0f,
            maxStaffBonus = 0,
            salaryMultiplier = 1.0f,

            upgradeCostMultiplier = 1.0f,
            fineMultiplier = 1.0f,
            taxRateMod = 0f,
            passiveIncomePerDay = 0,

            scandalChanceMod = 1.0f,
            waitTimeMultiplier = 1.0f,

            guardSpeedMultiplier = 1.0f,
            guardSalaryMultiplier = 1.0f,
            guardStressMultiplier = 1.0f,

            registrationSpeedMultiplier = 1.0f,
            documentQualityMod = 0f,
            errorRateModifier = 1.0f,

            corruptionGrowthMod = 1.0f,

            nightClientMod = 1.0f,
            equipmentIdlePenalty = 0f
        };
    }

    // ----- Таблица всех 20 политик -----
    private static List<Entry> BuildEntries()
    {
        var list = new List<Entry>();

        // --- КАДРОВЫЕ (4) ---

        // 1. Policy_QuickHire
        var p1 = New(Policy_QuickHire,
            "Ускоренный найм",
            "Кандидаты появляются в два раза быстрее, но нанять их дороже.",
            "УКАЗ №201: О срочном привлечении кадров");
        p1.hiringSpeedMultiplier = 2.0f;
        p1.hiringCostMultiplier = 1.2f;
        list.Add(p1);

        // 2. Policy_FlexSchedule
        var p2 = New(Policy_FlexSchedule,
            "Гибкий график",
            "Сотрудники медленнее устают, но мораль падает быстрее.",
            "УКАЗ №202: О свободном распорядке");
        p2.energyLossMultiplier = 0.7f;
        p2.moraleLossMultiplier = 1.3f;
        list.Add(p2);

        // 3. Policy_StaffReserve
        var p3 = New(Policy_StaffReserve,
            "Кадровый резерв",
            "Можно нанять одного дополнительного сотрудника, но зарплаты выше.",
            "УКАЗ №203: О расширении штата");
        p3.maxStaffBonus = 1;
        p3.salaryMultiplier = 1.1f;
        list.Add(p3);

        // 4. Policy_TotalControl
        var p4 = New(Policy_TotalControl,
            "Тотальный контроль",
            "Ошибки сотрудников сокращаются, но стресс растёт быстрее.",
            "УКАЗ №204: О неусыпном надзоре");
        p4.stressGrowthMultiplier = 1.2f;
        p4.errorRateModifier = 0.7f;
        list.Add(p4);

        // --- ЭКОНОМИЧЕСКИЕ (4) ---

        // 5. Policy_TaxOptimization
        var p5 = New(Policy_TaxOptimization,
            "Налоговая оптимизация",
            "Доход растёт, но риск проверок увеличивается.",
            "УКАЗ №301: О пересмотре налоговой базы");
        p5.incomeMultiplier = 1.15f;
        p5.inspectionRiskMod = 0.1f;
        list.Add(p5);

        // 6. Policy_InvestmentFund
        var p6 = New(Policy_InvestmentFund,
            "Инвестиционный фонд",
            "Ежедневный пассивный доход, но апгрейды дорожают.",
            "УКАЗ №302: О создании резервного фонда");
        p6.passiveIncomePerDay = 50;
        p6.upgradeCostMultiplier = 1.15f;
        list.Add(p6);

        // 7. Policy_HardEconomy
        var p7 = New(Policy_HardEconomy,
            "Жёсткая экономия",
            "Зарплаты сокращены, но сотрудники быстрее устают.",
            "УКАЗ №303: О режиме строгой экономии");
        p7.salaryMultiplier = 0.9f;
        p7.energyLossMultiplier = 1.2f;
        list.Add(p7);

        // 8. Policy_FinancialImmunity
        var p8 = New(Policy_FinancialImmunity,
            "Финансовый иммунитет",
            "Штрафы снижены вдвое, но налог на прибыль растёт.",
            "УКАЗ №304: О защите от фискальных взысканий");
        p8.fineMultiplier = 0.5f;
        p8.taxRateMod = 0.1f;
        list.Add(p8);

        // --- СИЛОВЫЕ (4) ---

        // 9. Policy_Patrol
        var p9 = New(Policy_Patrol,
            "Усиленный патруль",
            "Клиенты реже скандалят, но охранники двигаются медленнее.",
            "УКАЗ №401: О повышенной бдительности");
        p9.scandalChanceMod = 0.6f;
        p9.guardSpeedMultiplier = 0.8f;
        list.Add(p9);

        // 10. Policy_Curfew
        var p10 = New(Policy_Curfew,
            "Комендантский час",
            "Ночью клиенты не приходят, но оборудование простаивает.",
            "УКАЗ №402: О ночном режиме");
        p10.nightClientMod = 0.0f;
        p10.equipmentIdlePenalty = 0.2f;
        list.Add(p10);

        // 11. Policy_ViolationImmunity
        var p11 = New(Policy_ViolationImmunity,
            "Иммунитет к нарушениям",
            "Штрафы за нарушения снижены, но охранники выгорают быстрее.",
            "УКАЗ №403: О снисхождении к малым проступкам");
        p11.fineMultiplier = 0.5f;
        p11.guardStressMultiplier = 1.2f;
        list.Add(p11);

        // 12. Policy_TotalSecurity
        var p12 = New(Policy_TotalSecurity,
            "Тотальная безопасность",
            "Клиенты никогда не скандалят, но охранники требуют вдвое больше.",
            "УКАЗ №404: Об абсолютной безопасности");
        p12.scandalChanceMod = 0.0f;
        p12.guardSalaryMultiplier = 2.0f;
        list.Add(p12);

        // --- БЮРОКРАТИЧЕСКИЕ (4) ---

        // 13. Policy_Paperwork
        var p13 = New(Policy_Paperwork,
            "Бумажная волокита",
            "Доход с каждого клиента выше, но ждать приходится дольше.",
            "УКАЗ №501: О тщательной проверке");
        p13.incomeMultiplier = 1.1f;
        p13.waitTimeMultiplier = 1.3f;
        list.Add(p13);

        // 14. Policy_FastRegistration
        var p14 = New(Policy_FastRegistration,
            "Ускоренная регистрация",
            "Регистрация быстрее, но качество документов ниже.",
            "УКАЗ №502: О скоростном оформлении");
        p14.registrationSpeedMultiplier = 1.4f;
        p14.documentQualityMod = -0.2f;
        list.Add(p14);

        // 15. Policy_Bureaucracy
        var p15 = New(Policy_Bureaucracy,
            "Тотальная бюрократия",
            "Меньше ошибок, но все действия занимают больше времени.",
            "УКАЗ №503: О всеобъемлющем регламенте");
        p15.workSpeedMultiplier = 0.75f;
        p15.stressGrowthMultiplier = 0.7f;
        p15.errorRateModifier = 0.7f;
        list.Add(p15);

        // 16. Policy_RegionalBonus
        var p16 = New(Policy_RegionalBonus,
            "Региональный бонус",
            "Доход от всех регионов выше.",
            "УКАЗ №504: О развитии подведомственных территорий");
        p16.incomeMultiplier = 1.2f;
        list.Add(p16);

        // --- ТЕНЕВЫЕ (3) ---

        // 17. Policy_ShadowFunding
        var p17 = New(Policy_ShadowFunding,
            "Теневое финансирование",
            "Найм дешевле, но коррупция растёт быстрее.",
            "УКАЗ №601: О неформальных источниках");
        p17.hiringCostMultiplier = 0.7f;
        p17.corruptionGrowthMod = 1.5f;
        list.Add(p17);

        // 18. Policy_DebtForgiveness
        var p18 = New(Policy_DebtForgiveness,
            "Списание долгов",
            "Один штраф в день списывается бесплатно.",
            "УКАЗ №602: О прощении");
        p18.behaviorFlag = "DAILY_FINE_WAIVE";
        list.Add(p18);

        // 19. Policy_Forgery
        var p19 = New(Policy_Forgery,
            "Подделка",
            "Документы можно оформлять мгновенно, но коррупция растёт.",
            "УКАЗ №603: О мгновенном оформлении");
        p19.behaviorFlag = "INSTANT_DOC";
        p19.corruptionGrowthMod = 1.3f;
        list.Add(p19);

        // --- МИНИСТЕРСКАЯ (1) ---

        // 20. Policy_MinisterImmunity
        var p20 = New(Policy_MinisterImmunity,
            "Иммунитет Министра",
            "Ошибки не начисляют страйки. Весь доход идёт в карман. Коррупция не растёт.",
            "УКАЗ №1000: О правах Министра");
        p20.behaviorFlag = "MINISTER_IMMUNITY";
        p20.errorRateModifier = 0.0f;
        p20.incomeMultiplier = 1.0f;
        list.Add(p20);

        return list;
    }
}
#endif

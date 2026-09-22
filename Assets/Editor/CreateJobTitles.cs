// Assets/Editor/CreateJobTitles.cs
// Редакторский скрипт для пакетного создания / обновления ассетов JobTitleData
// (должности карьерного дерева) в Resources/Progression/Jobs/.
//
// Меню:
//   Tools/Bureau/Create Job Titles              — идемпотентное создание/обновление
//   Tools/Bureau/Create Job Titles (Force)      — удалить и создать заново
//
// Поведение:
//   1. Ищет существующие JobTitleData-ассеты по jobID через AssetDatabase.FindAssets.
//   2. Если найден — перезаписывает поля актуальными значениями (не дублирует).
//   3. Если не найден — создаёт новый ассет через ScriptableObject.CreateInstance.
//   4. requiredPreviousJob связывается вторым проходом (после создания всех ассетов).
//   5. Не трогает ассеты JobTitleData, чей jobID не входит в список ниже.
//
// Шаблон: CreateAccountantAssets.cs (идемпотентность, EnsureFolder, двух-проходная связь)
//          CreateAchievementAssets.cs (data-driven список Entry, счётчики в лог).
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Scriptables.Progression;
using UnityEditor;
using UnityEngine;

public static class CreateJobTitles
{
    // ----- ID константы должностей -----
    public const string Job_Director         = "Job_Director";
    public const string Job_KeeperOfSeals    = "Job_KeeperOfSeals";
    public const string Job_FormularMaster   = "Job_FormularMaster";
    public const string Job_Treasurer        = "Job_Treasurer";
    public const string Job_ChancelleryHead  = "Job_ChancelleryHead";
    public const string Job_StaffOverseer    = "Job_StaffOverseer";
    public const string Job_OrderGuard       = "Job_OrderGuard";
    public const string Job_ManagerOfAffairs = "Job_ManagerOfAffairs";
    public const string Job_RegionalManager  = "Job_RegionalManager";
    public const string Job_OrderKeeper      = "Job_OrderKeeper";
    public const string Job_DepartmentHead   = "Job_DepartmentHead";
    public const string Job_Minister         = "Job_Minister";

    private const string Folder = "Assets/Resources/Progression/Jobs";
    private const string LogTag = "[CreateJobTitles]";

    [MenuItem("Tools/Bureau/Create Job Titles")]
    public static void CreateAll() => Run(forceRecreate: false);

    [MenuItem("Tools/Bureau/Create Job Titles (Force)")]
    public static void CreateAllForce() => Run(forceRecreate: true);

    private static void Run(bool forceRecreate)
    {
        EnsureFolder(Folder);

        var entries = BuildEntries();
        var byID = new Dictionary<string, JobTitleData>();

        int created = 0;
        int updated = 0;

        // ----- Первый проход: создать/обновить ассеты, requiredPreviousJob пока null -----
        foreach (var e in entries)
        {
            string path = $"{Folder}/{e.jobID}.asset";
            var existing = FindByID(e.jobID);

            JobTitleData asset;
            bool isNew = (existing == null);

            if (forceRecreate && existing != null)
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(existing));
                isNew = true;
            }

            if (isNew)
            {
                asset = ScriptableObject.CreateInstance<JobTitleData>();
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
            byID[e.jobID] = asset;
            EditorUtility.SetDirty(asset);
        }

        // ----- Второй проход: связать requiredPreviousJob -----
        int linked = 0;
        foreach (var e in entries)
        {
            if (string.IsNullOrEmpty(e.requiredPreviousJobID)) continue;
            if (!byID.TryGetValue(e.jobID, out var asset) || asset == null) continue;
            if (!byID.TryGetValue(e.requiredPreviousJobID, out var prev) || prev == null) continue;

            if (asset.requiredPreviousJob != prev)
            {
                asset.requiredPreviousJob = prev;
                EditorUtility.SetDirty(asset);
                linked++;
                Debug.Log($"{LogTag} Связь: {asset.jobID} -> previous = {prev.jobID}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"{LogTag} Готово. Создано: {created}, обновлено: {updated}, связано: {linked} (force={forceRecreate}).");
    }

    // ----- Заполнение полей ассета данными из Entry -----
    private static void ApplyEntry(JobTitleData asset, Entry e)
    {
        asset.jobID = e.jobID;
        asset.titleName = e.titleName;
        asset.description = e.description;
        asset.tierLevel = e.tierLevel;
        asset.costInfluence = e.costInfluence;
        asset.costMoney = e.costMoney;
        asset.requiredCapturedRegionsCount = e.requiredCapturedRegionsCount;
        asset.isMinisterPosition = e.isMinisterPosition;
        asset.caseTitle = e.caseTitle;
        asset.isMandatory = e.isMandatory;
        asset.policyID = e.policyID;

        asset.unlockedRoles = e.unlockedRoles != null
            ? new List<StaffController.Role>(e.unlockedRoles)
            : new List<StaffController.Role>();

        asset.unlockedUpgrades = e.unlockedUpgrades != null
            ? new List<string>(e.unlockedUpgrades)
            : new List<string>();

        asset.correctPath = CopyPath(e.correctPath);
        asset.alternatePath = CopyPath(e.alternatePath);

        // requiredPreviousJob свяжется во втором проходе
        if (string.IsNullOrEmpty(e.requiredPreviousJobID))
        {
            asset.requiredPreviousJob = null;
        }
    }

    private static JobTitleData.PathData CopyPath(JobTitleData.PathData src)
    {
        if (src == null) return new JobTitleData.PathData();
        return new JobTitleData.PathData
        {
            isValid = src.isValid,
            requiredDays = src.requiredDays,
            requiredClients = src.requiredClients,
            requiredStaff = src.requiredStaff,
            requiredRegions = src.requiredRegions,
            requiredUpgrades = src.requiredUpgrades,
            requiredDocuments = src.requiredDocuments,
            requiredMoney = src.requiredMoney,
            moneyCost = src.moneyCost,
            corruptionCost = src.corruptionCost
        };
    }

    // ----- Поиск существующего ассета по jobID -----
    private static JobTitleData FindByID(string jobID)
    {
        string[] guids = AssetDatabase.FindAssets("t:JobTitleData");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<JobTitleData>(path);
            if (asset != null && asset.jobID == jobID) return asset;
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

    // ----- Описание одной должности -----
    private struct Entry
    {
        public string jobID;
        public string titleName;
        public string description;
        public int tierLevel;
        public string requiredPreviousJobID;
        public int costInfluence;
        public int costMoney;
        public int requiredCapturedRegionsCount;
        public bool isMinisterPosition;
        public bool isMandatory;
        public string caseTitle;
        public string policyID;
        public List<StaffController.Role> unlockedRoles;
        public List<string> unlockedUpgrades;
        public JobTitleData.PathData correctPath;
        public JobTitleData.PathData alternatePath;
    }

    // ----- Таблица всех 12 должностей карьерного дерева -----
    private static List<Entry> BuildEntries()
    {
        var list = new List<Entry>
        {
            // 0. Директор Бюро (стартовая)
            new Entry
            {
                jobID = Job_Director,
                titleName = "Директор Бюро",
                description = "Стартовая должность. Без неё карьерное дерево закрыто.",
                tierLevel = 0,
                requiredPreviousJobID = "",
                costInfluence = 0,
                costMoney = 0,
                requiredCapturedRegionsCount = 0,
                isMinisterPosition = false,
                isMandatory = true,
                caseTitle = "Дело о начале пути",
                policyID = "",
                unlockedRoles = new List<StaffController.Role> { StaffController.Role.Intern },
                unlockedUpgrades = new List<string>(),
                correctPath = new JobTitleData.PathData { isValid = true, requiredDays = 0 },
                alternatePath = new JobTitleData.PathData { isValid = false }
            },

            // 1. Хранитель печатей
            new Entry
            {
                jobID = Job_KeeperOfSeals,
                titleName = "Хранитель печатей",
                description = "Контролирует все входящие и исходящие документы. Открывает роль Регистратора.",
                tierLevel = 1,
                requiredPreviousJobID = Job_Director,
                costInfluence = 0,
                costMoney = 0,
                requiredCapturedRegionsCount = 0,
                isMinisterPosition = false,
                isMandatory = true,
                caseTitle = "Дело о печатях",
                policyID = "Policy_QuickHire",
                unlockedRoles = new List<StaffController.Role> { StaffController.Role.Registrar },
                unlockedUpgrades = new List<string>(),
                correctPath = new JobTitleData.PathData { isValid = true, requiredDays = 3, requiredClients = 15 },
                alternatePath = new JobTitleData.PathData { isValid = true, requiredDays = 3, moneyCost = 300, corruptionCost = 5 }
            },

            // 2. Магистр формуляров
            new Entry
            {
                jobID = Job_FormularMaster,
                titleName = "Магистр формуляров",
                description = "Знаток бланков и шаблонов. Открывает роль Клерка и апгрейд 'DocumentDesk'.",
                tierLevel = 2,
                requiredPreviousJobID = Job_KeeperOfSeals,
                costInfluence = 0,
                costMoney = 0,
                requiredCapturedRegionsCount = 0,
                isMinisterPosition = false,
                isMandatory = false,
                caseTitle = "Дело о формулярах",
                policyID = "Policy_Paperwork",
                unlockedRoles = new List<StaffController.Role> { StaffController.Role.Clerk },
                unlockedUpgrades = new List<string> { "Upgrade_DocumentDesk" },
                correctPath = new JobTitleData.PathData { isValid = true, requiredDays = 5, requiredStaff = 2, requiredDocuments = 20 },
                alternatePath = new JobTitleData.PathData { isValid = true, requiredDays = 5, moneyCost = 500, corruptionCost = 10 }
            },

            // 3. Казначей
            new Entry
            {
                jobID = Job_Treasurer,
                titleName = "Казначей",
                description = "Хранитель казны Бюро. Открывает роль Кассира и апгрейд 'Safe'.",
                tierLevel = 2,
                requiredPreviousJobID = Job_KeeperOfSeals,
                costInfluence = 0,
                costMoney = 0,
                requiredCapturedRegionsCount = 0,
                isMinisterPosition = false,
                isMandatory = false,
                caseTitle = "Дело о казне",
                policyID = "Policy_TaxOptimization",
                unlockedRoles = new List<StaffController.Role> { StaffController.Role.Cashier },
                unlockedUpgrades = new List<string> { "Upgrade_Safe" },
                correctPath = new JobTitleData.PathData { isValid = true, requiredDays = 5, requiredMoney = 500, requiredClients = 30 },
                alternatePath = new JobTitleData.PathData { isValid = true, requiredDays = 5, moneyCost = 800, corruptionCost = 10 }
            },

            // 4. Начальник канцелярии (объединяет Магистра формуляров и Казначея)
            new Entry
            {
                jobID = Job_ChancelleryHead,
                titleName = "Начальник канцелярии",
                description = "Глава документооборота. Доступен из 'Магистр формуляров' (основной) или 'Казначей' (альтернативный путь — реализуется в ProgressionManager).",
                tierLevel = 3,
                requiredPreviousJobID = Job_FormularMaster, // логика OR будет в ProgressionManager
                costInfluence = 0,
                costMoney = 0,
                requiredCapturedRegionsCount = 1,
                isMinisterPosition = false,
                isMandatory = true,
                caseTitle = "Дело о канцелярии",
                policyID = "Policy_Bureaucracy",
                unlockedRoles = new List<StaffController.Role> { StaffController.Role.Archivist },
                unlockedUpgrades = new List<string> { "Upgrade_ArchiveCabinet", "Upgrade_DocCat2" },
                correctPath = new JobTitleData.PathData { isValid = true, requiredDays = 7, requiredStaff = 4, requiredRegions = 1 },
                alternatePath = new JobTitleData.PathData { isValid = true, requiredDays = 7, moneyCost = 1000, corruptionCost = 15 }
            },

            // 5. Смотритель штата
            new Entry
            {
                jobID = Job_StaffOverseer,
                titleName = "Смотритель штата",
                description = "Следит за кадрами и графиками. Открывает роль Офис-менеджера и апгрейд 'RestRoom'.",
                tierLevel = 4,
                requiredPreviousJobID = Job_ChancelleryHead,
                costInfluence = 0,
                costMoney = 0,
                requiredCapturedRegionsCount = 1,
                isMinisterPosition = false,
                isMandatory = false,
                caseTitle = "Дело о штате",
                policyID = "Policy_FlexSchedule",
                unlockedRoles = new List<StaffController.Role> { StaffController.Role.OfficeManager },
                unlockedUpgrades = new List<string> { "Upgrade_RestRoom" },
                correctPath = new JobTitleData.PathData { isValid = true, requiredDays = 10, requiredStaff = 6, requiredUpgrades = 2 },
                alternatePath = new JobTitleData.PathData { isValid = true, requiredDays = 10, moneyCost = 1500, corruptionCost = 15 }
            },

            // 6. Страж порядка
            new Entry
            {
                jobID = Job_OrderGuard,
                titleName = "Страж порядка",
                description = "Обеспечивает безопасность Бюро. Открывает роль Охранника и апгрейд 'GuardPost'.",
                tierLevel = 4,
                requiredPreviousJobID = Job_ChancelleryHead,
                costInfluence = 0,
                costMoney = 0,
                requiredCapturedRegionsCount = 2,
                isMinisterPosition = false,
                isMandatory = false,
                caseTitle = "Дело о порядке",
                policyID = "Policy_Patrol",
                unlockedRoles = new List<StaffController.Role> { StaffController.Role.Guard },
                unlockedUpgrades = new List<string> { "Upgrade_GuardPost" },
                correctPath = new JobTitleData.PathData { isValid = true, requiredDays = 10, requiredClients = 50, requiredRegions = 2 },
                alternatePath = new JobTitleData.PathData { isValid = true, requiredDays = 10, moneyCost = 1200, corruptionCost = 15 }
            },

            // 7. Управляющий делами (объединяет Смотрителя штата и Стража порядка)
            new Entry
            {
                jobID = Job_ManagerOfAffairs,
                titleName = "Управляющий делами",
                description = "Координатор всех служб. Доступен из 'Страж порядка' (основной) или 'Смотритель штата' (альтернативный путь — реализуется в ProgressionManager).",
                tierLevel = 5,
                requiredPreviousJobID = Job_OrderGuard, // логика OR будет в ProgressionManager
                costInfluence = 0,
                costMoney = 0,
                requiredCapturedRegionsCount = 3,
                isMinisterPosition = false,
                isMandatory = true,
                caseTitle = "Дело об управлении",
                policyID = "Policy_StaffReserve",
                unlockedRoles = new List<StaffController.Role> { StaffController.Role.Accountant },
                unlockedUpgrades = new List<string> { "Upgrade_AccountingDesk", "Upgrade_CoffeeMachine" },
                correctPath = new JobTitleData.PathData { isValid = true, requiredDays = 14, requiredStaff = 8, requiredRegions = 3 },
                alternatePath = new JobTitleData.PathData { isValid = true, requiredDays = 14, moneyCost = 2000, corruptionCost = 20 }
            },

            // 8. Региональный распорядитель
            new Entry
            {
                jobID = Job_RegionalManager,
                titleName = "Региональный распорядитель",
                description = "Контролирует филиалы в регионах. Разблокирует 'Copier' и 'PhoneLine'.",
                tierLevel = 6,
                requiredPreviousJobID = Job_ManagerOfAffairs,
                costInfluence = 0,
                costMoney = 0,
                requiredCapturedRegionsCount = 4,
                isMinisterPosition = false,
                isMandatory = false,
                caseTitle = "Дело о регионах",
                policyID = "Policy_RegionalBonus",
                unlockedRoles = new List<StaffController.Role>(),
                unlockedUpgrades = new List<string> { "Upgrade_Copier", "Upgrade_PhoneLine" },
                correctPath = new JobTitleData.PathData { isValid = true, requiredDays = 18, requiredRegions = 4, requiredUpgrades = 5 },
                alternatePath = new JobTitleData.PathData { isValid = true, requiredDays = 18, moneyCost = 3000, corruptionCost = 20 }
            },

            // 9. Хранитель порядка
            new Entry
            {
                jobID = Job_OrderKeeper,
                titleName = "Хранитель порядка",
                description = "Гарант тишины и дисциплины. Разблокирует 'Cooler'.",
                tierLevel = 6,
                requiredPreviousJobID = Job_ManagerOfAffairs,
                costInfluence = 0,
                costMoney = 0,
                requiredCapturedRegionsCount = 3,
                isMinisterPosition = false,
                isMandatory = false,
                caseTitle = "Дело о тишине",
                policyID = "Policy_TotalSecurity",
                unlockedRoles = new List<StaffController.Role>(),
                unlockedUpgrades = new List<string> { "Upgrade_Cooler" },
                correctPath = new JobTitleData.PathData { isValid = true, requiredDays = 18, requiredClients = 100, requiredRegions = 3 },
                alternatePath = new JobTitleData.PathData { isValid = true, requiredDays = 18, moneyCost = 2500, corruptionCost = 20 }
            },

            // 10. Глава департамента (объединяет Регионального распорядителя и Хранителя порядка)
            new Entry
            {
                jobID = Job_DepartmentHead,
                titleName = "Глава департамента",
                description = "Высший руководитель среднего звена. Доступен из 'Региональный распорядитель' (основной) или 'Хранитель порядка' (альтернативный путь — реализуется в ProgressionManager).",
                tierLevel = 7,
                requiredPreviousJobID = Job_RegionalManager, // логика OR будет в ProgressionManager
                costInfluence = 0,
                costMoney = 0,
                requiredCapturedRegionsCount = 5,
                isMinisterPosition = false,
                isMandatory = true,
                caseTitle = "Дело о департаменте",
                policyID = "Policy_TotalControl",
                unlockedRoles = new List<StaffController.Role>(),
                unlockedUpgrades = new List<string> { "Upgrade_DocCat3" },
                correctPath = new JobTitleData.PathData { isValid = true, requiredDays = 21, requiredStaff = 10, requiredRegions = 5 },
                alternatePath = new JobTitleData.PathData { isValid = true, requiredDays = 21, moneyCost = 4000, corruptionCost = 25 }
            },

            // 11. Министр (финал, только правильный путь)
            new Entry
            {
                jobID = Job_Minister,
                titleName = "Министр",
                description = "Вершина карьеры. Победа в игре. Доступен только правильным путём из 'Глава департамента'.",
                tierLevel = 8,
                requiredPreviousJobID = Job_DepartmentHead,
                costInfluence = 0,
                costMoney = 0,
                requiredCapturedRegionsCount = 6,
                isMinisterPosition = true,
                isMandatory = true,
                caseTitle = "Дело о власти",
                policyID = "Policy_MinisterImmunity",
                unlockedRoles = new List<StaffController.Role>(),
                unlockedUpgrades = new List<string>(),
                correctPath = new JobTitleData.PathData
                {
                    isValid = true,
                    requiredDays = 28,
                    requiredStaff = 12,
                    requiredRegions = 6,
                    requiredClients = 200,
                    requiredUpgrades = 8
                },
                alternatePath = new JobTitleData.PathData { isValid = false }
            }
        };

        return list;
    }
}
#endif

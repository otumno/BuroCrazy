// Assets/Scripts/Managers/ProgressionManager.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Scriptables.Progression;
using Data.Documents;
using Data.Calendar; 
using Enums;

namespace Managers
{
    public class ProgressionManager : MonoBehaviour
    {
        public static ProgressionManager Instance { get; private set; }

        [Header("Ресурсы")]
        [SerializeField] private int currentInfluence = 0;

        [Header("База Данных (Заполнить в Инспекторе!)")]
        public List<RegionData> allRegionsDatabase;
        public List<JobTitleData> allJobsDatabase;

        // --- Состояние Прогресса (Runtime) ---
        // Храним ID открытых регионов и должностей (строки легче сохранять в JSON)
        private HashSet<string> unlockedRegionIDs = new HashSet<string>();
        private HashSet<string> unlockedJobIDs = new HashSet<string>();

        // --- Состояние регионов для системы потоков ---
        [Header("Потоки клиентов")]
        [Tooltip("Runtime состояние всех регионов (автоматически создаётся при старте)")]
        public List<Scriptables.Progression.RegionRuntimeState> regionRuntimeStates = new List<Scriptables.Progression.RegionRuntimeState>();

        // События для UI
        public event System.Action<int> OnInfluenceChanged;
        public event System.Action OnProgressionUpdated; // Срабатывает при любом открытии региона/должности
        public event System.Action OnDailyFlowUpdated; // Срабатывает при обновлении потоков в новый день

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Инициализируем состояние регионов
            InitializeRegionStates();

            // Подписываемся на смену дня
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnDayChanged += OnDayChanged;
                Debug.Log("[ProgressionManager] Подписан на TimeManager.OnDayChanged");
            }
            else
            {
                Debug.LogWarning("[ProgressionManager] TimeManager.Instance не найден! Daily flow не будет обновляться.");
            }
        }

        /// <summary>
        /// Инициализирует runtime состояния для всех регионов из базы
        /// </summary>
        private void InitializeRegionStates()
        {
            regionRuntimeStates.Clear();

            if (allRegionsDatabase == null || allRegionsDatabase.Count == 0)
            {
                Debug.LogWarning("[ProgressionManager] allRegionsDatabase пуст!");
                return;
            }

            int currentDay = TimeManager.Instance != null ? TimeManager.Instance.GetCurrentDay() : 1;

            foreach (var region in allRegionsDatabase)
            {
                if (region == null) continue;

                var state = new Scriptables.Progression.RegionRuntimeState
                {
                    data = region,
                    isUnlocked = false,
                    daysOwned = 0,
                    currentDailyTarget = 0
                };

                // Если регион уже открыт (например, стартовый регион)
                if (IsRegionUnlocked(region.regionID))
                {
                    // Восстанавливаем состояние на основе количества захваченных дней
                    // Для стартового региона считаем что он был захвачен в день 1
                    state = Scriptables.Progression.RegionRuntimeState.CreateUnlocked(region, currentDay);
                    state.daysOwned = Mathf.Min(state.daysOwned, region.rampUpDays);
                    state.currentDailyTarget = Scriptables.Progression.RegionRuntimeState.CalculateDailyTarget(region, state.daysOwned);

                    Debug.Log($"[ProgressionManager] Восстановлено состояние '{region.displayName}': " +
                              $"daysOwned={state.daysOwned}, target={state.currentDailyTarget}");
                }

                regionRuntimeStates.Add(state);
            }

            Debug.Log($"[ProgressionManager] Инициализировано {regionRuntimeStates.Count} регионов");
        }

        /// <summary>
        /// Вызывается при смене дня - обновляет потоки всех регионов
        /// </summary>
        private void OnDayChanged(int newDay)
        {
            Debug.Log($"[ProgressionManager] === Новый день: {newDay} ===");

            int totalDailyFlow = 0;

            foreach (var state in regionRuntimeStates)
            {
                if (state.isUnlocked)
                {
                    // Увеличиваем daysOwned и пересчитываем цель
                    state.OnNewDay(state.daysOwned + 1);
                    totalDailyFlow += state.currentDailyTarget;

                    Debug.Log($"[ProgressionManager] Регион '{state.data?.displayName}': " +
                              $"{state.previousDayFlow} -> {state.currentDailyTarget} клиентов/день");
                }
            }

            Debug.Log($"[ProgressionManager] Общий дневной поток: {totalDailyFlow} клиентов");
            OnDailyFlowUpdated?.Invoke();
        }

        #region Influence Logic

        public int GetInfluence() => currentInfluence;

        public void AddInfluence(int amount)
        {
            if (amount == 0) return;
            currentInfluence += amount;
            
            // ИСПРАВЛЕНИЕ: Добавлены скобки вокруг тернарного оператора (amount > 0 ? "+" : "")
            Debug.Log($"[Progression] Влияние изменено: {(amount > 0 ? "+" : "")}{amount}. Всего: {currentInfluence}");
            
            OnInfluenceChanged?.Invoke(currentInfluence);
        }

        public bool TrySpendInfluence(int amount)
        {
            if (currentInfluence >= amount)
            {
                AddInfluence(-amount);
                return true;
            }
            return false;
        }

        #endregion

        #region Region Logic

        public bool IsRegionUnlocked(string regionID) => unlockedRegionIDs.Contains(regionID);

        public void UnlockRegion(string regionID)
        {
            if (string.IsNullOrEmpty(regionID)) return;
            if (IsRegionUnlocked(regionID)) 
            {
                Debug.Log($"[ProgressionManager] Район {regionID} уже открыт.");
                return;
            }

            RegionData region = allRegionsDatabase?.Find(r => r != null && r.regionID == regionID);
            if (region != null)
            {
                FinalizeRegionUnlock(region);
                Debug.Log($"[ProgressionManager] Район {regionID} открыт через создание директора.");
            }
            else
            {
                Debug.LogWarning($"[ProgressionManager] RegionData не найден для ID: {regionID}");
            }
        }

        public int GetCapturedRegionsCount() => unlockedRegionIDs.Count;

        // Этот метод будет вызываться, когда документ о захвате успешно обработан
        public void FinalizeRegionUnlock(RegionData region)
        {
            if (region == null) return;
            if (IsRegionUnlocked(region.regionID)) return;

            unlockedRegionIDs.Add(region.regionID);
            Debug.Log($"<color=green>[Progression] РЕГИОН ЗАХВАЧЕН: {region.displayName}</color>");

            // Разблокируем телефонные контакты, если привязаны
            if (region.unlocksContactIDs != null)
            {
                foreach (string contactID in region.unlocksContactIDs)
                {
                    if (!string.IsNullOrEmpty(contactID))
                        PhoneManager.Instance?.UnlockContact(contactID);
                }
            }

            // Создаём runtime state для региона
            CreateRegionRuntimeState(region);

            OnProgressionUpdated?.Invoke();

            // Здесь в будущем можно вызвать NotificationUI
        }

        /// <summary>
        /// Создаёт runtime state для региона и добавляет в коллекцию
        /// </summary>
        private void CreateRegionRuntimeState(RegionData region)
        {
            // Проверяем не существует ли уже state для этого региона
            var existingState = regionRuntimeStates.Find(s => s.data == region);
            if (existingState != null)
            {
                existingState.isUnlocked = true;
                existingState.daysOwned = 1; // Первый день - 25%
                existingState.currentDailyTarget = Scriptables.Progression.RegionRuntimeState.CalculateDailyTarget(region, 1);
                existingState.unlockTime = Time.time;
                existingState.unlockDay = TimeManager.Instance != null ? TimeManager.Instance.GetCurrentDay() : 1;

                Debug.Log($"[ProgressionManager] Обновлён существующий state для '{region.displayName}': " +
                          $"target={existingState.currentDailyTarget}");
            }
            else
            {
                // Создаём новый state
                int currentDay = TimeManager.Instance != null ? TimeManager.Instance.GetCurrentDay() : 1;
                var newState = Scriptables.Progression.RegionRuntimeState.CreateUnlocked(region, currentDay);
                regionRuntimeStates.Add(newState);

                Debug.Log($"[ProgressionManager] Создан новый state для '{region.displayName}'");
            }

            // Уведомляем об изменении потоков
            OnDailyFlowUpdated?.Invoke();
        }

        /// <summary>
        /// Получает runtime state для конкретного региона
        /// </summary>
        public Scriptables.Progression.RegionRuntimeState GetRegionRuntimeState(string regionID)
        {
            return regionRuntimeStates.Find(s => s.data != null && s.data.regionID == regionID);
        }

        /// <summary>
        /// Получает runtime state для конкретного региона по данным
        /// </summary>
        public Scriptables.Progression.RegionRuntimeState GetRegionRuntimeState(RegionData region)
        {
            if (region == null) return null;
            return regionRuntimeStates.Find(s => s.data == region);
        }

        /// <summary>
        /// Получает все активные (разблокированные) регионы
        /// </summary>
        public List<Scriptables.Progression.RegionRuntimeState> GetActiveRegions()
        {
            return regionRuntimeStates.FindAll(s => s.isUnlocked);
        }

        /// <summary>
        /// Получает общее количество клиентов в день от всех регионов
        /// </summary>
        public int GetTotalDailyFlow()
        {
            int total = 0;
            foreach (var state in regionRuntimeStates)
            {
                if (state.isUnlocked)
                {
                    total += state.currentDailyTarget;
                }
            }
            return total;
        }

        /// <summary>
        /// Получает общее количество клиентов в день для конкретной группы архетипов
        /// </summary>
        public int GetDailyFlowForGroup(string groupID)
        {
            int total = 0;

            // Проверяем есть ли такая группа в данных региона
            var regionState = regionRuntimeStates.Find(s =>
                s.isUnlocked &&
                s.data != null &&
                s.data.archetypeGroups != null &&
                s.data.archetypeGroups.Contains(groupID));

            if (regionState == null)
            {
                // Группа может быть в нескольких регионах - суммируем
                foreach (var state in regionRuntimeStates)
                {
                    if (state.isUnlocked && state.data != null && state.data.groupWeights != null)
                    {
                        var weight = state.data.groupWeights.Find(w => w.groupID == groupID);
                        if (weight != null)
                        {
                            // Пропорционально весу группы
                            total += Mathf.RoundToInt(state.currentDailyTarget * weight.weight);
                        }
                    }
                }
            }
            else
            {
                // Простой случай - группа принадлежит одному региону
                var weight = regionState.data.groupWeights?.Find(w => w.groupID == groupID);
                if (weight != null)
                {
                    total = Mathf.RoundToInt(regionState.currentDailyTarget * weight.weight);
                }
            }

            return total;
        }

        /// <summary>
        /// Получает суммарные веса групп из всех активных регионов
        /// </summary>
        public Dictionary<string, float> GetCombinedGroupWeights()
        {
            var combined = new Dictionary<string, float>();

            foreach (var state in regionRuntimeStates)
            {
                if (!state.isUnlocked) continue;
                if (state.data == null || state.data.groupWeights == null) continue;

                foreach (var weight in state.data.groupWeights)
                {
                    if (combined.ContainsKey(weight.groupID))
                    {
                        combined[weight.groupID] += weight.weight;
                    }
                    else
                    {
                        combined[weight.groupID] = weight.weight;
                    }
                }
            }

            return combined;
        }

        /// <summary>
        /// Выбирает группу архетипов с учётом весов всех регионов
        /// </summary>
        public string SelectArchetypeGroupWeighted()
        {
            var weights = GetCombinedGroupWeights();
            if (weights.Count == 0) return "Default";

            // Weighted random
            float totalWeight = 0f;
            foreach (var kvp in weights)
            {
                totalWeight += kvp.Value;
            }

            float randomPoint = UnityEngine.Random.Range(0, totalWeight);
            float currentWeight = 0f;

            foreach (var kvp in weights)
            {
                currentWeight += kvp.Value;
                if (randomPoint <= currentWeight)
                {
                    return kvp.Key;
                }
            }

            return "Default";
        }

        // Метод для WaveManager: подсчет бонусов к спавну от всех открытых регионов
        public int GetTotalSpawnBonusForPeriod(CalendarDayPeriodType currentPeriod)
        {
            int totalBonus = 0;
            foreach (var region in allRegionsDatabase)
            {
                if (IsRegionUnlocked(region.regionID))
                {
                    // Ищем бонус для текущего периода (проверка битовой маски)
                    if (region.spawnBonuses != null)
                    {
                        var bonus = region.spawnBonuses.FirstOrDefault(b => (b.period & currentPeriod) != 0);
                        if (bonus != null)
                        {
                            totalBonus += bonus.additionalClients;
                        }
                    }
                }
            }
            return totalBonus;
        }

        #endregion

        #region Job Logic

        public bool IsJobUnlocked(string jobID) => unlockedJobIDs.Contains(jobID);

        // Проверка: можно ли начать процесс получения должности (создать документ)?
        public bool CanStartUnlockJob(JobTitleData job)
        {
            if (job == null) return false;
            if (IsJobUnlocked(job.jobID)) return false; // Уже открыто

            // 1. Проверка родителя (иерархия)
            if (job.requiredPreviousJob != null && !IsJobUnlocked(job.requiredPreviousJob.jobID))
                return false;

            // 2. Проверка количества регионов (Новое условие из плана)
            if (GetCapturedRegionsCount() < job.requiredCapturedRegionsCount)
                return false;

            // 3. Проверка ресурсов (влияние и деньги проверим в UI перед созданием документа, но можно и тут)
            // if (currentInfluence < job.costInfluence) return false;

            return true;
        }

        public void FinalizeJobPromotion(JobTitleData job)
        {
            if (job == null) return;
            if (IsJobUnlocked(job.jobID)) return;

            // 1. Отметить как открытую
            unlockedJobIDs.Add(job.jobID);
            Debug.Log($"<color=magenta>[Progression] ПОВЫШЕНИЕ ПОЛУЧЕНО: {job.titleName}</color>");

            // 2. Активировать политику (если есть)
            if (!string.IsNullOrEmpty(job.policyID) && PolicyManager.Instance != null)
            {
                PolicyManager.Instance.ActivatePolicy(job.policyID);
                Debug.Log($"[Progression] Политика активирована: {job.policyID}");
            }

            // 3. Открыть роли для найма (если есть)
            if (job.unlockedRoles != null && job.unlockedRoles.Count > 0)
            {
                // TODO: реальная разблокировка ролей — нужно дополнить HiringManager
                Debug.Log($"[Progression] Роли разблокированы: {string.Join(", ", job.unlockedRoles)}");
            }

            // 4. Активировать апгрейды (если есть) — сработают хуки
            if (job.unlockedUpgrades != null && job.unlockedUpgrades.Count > 0)
            {
                foreach (var upgradeID in job.unlockedUpgrades)
                {
                    if (UpgradeManager.Instance != null)
                    {
                        UpgradeManager.Instance.ActivateUpgradeByID(upgradeID);
                        Debug.Log($"[Progression] Апгрейд активирован: {upgradeID}");
                    }
                }
            }

            // 5. Уведомить подписчиков
            OnProgressionUpdated?.Invoke();

            if (job.isMinisterPosition)
            {
                Debug.Log("!!! ПОБЕДА !!! Игрок достиг ранга Министр.");
                // TODO: TriggerWinSequence();
            }
        }

        /// <summary>
        /// Запуск процесса повышения: проверка условий, списание денег (для alternate),
        /// создание ProjectDocumentDefinition и укладка на стол директора.
        /// </summary>
        public void StartJobPromotion(JobTitleData job, ProjectDocumentDefinition.PathType path)
        {
            if (job == null) return;
            if (IsJobUnlocked(job.jobID))
            {
                Debug.LogWarning($"[Progression] Должность {job.jobID} уже открыта.");
                return;
            }

            if (!CanStartUnlockJob(job))
            {
                Debug.LogWarning($"[Progression] Базовые условия для {job.jobID} не выполнены.");
                return;
            }

            // Проверяем валидность пути
            if (path == ProjectDocumentDefinition.PathType.Correct)
            {
                if (job.correctPath == null || !job.correctPath.isValid)
                {
                    Debug.LogWarning($"[Progression] Правильный путь для {job.jobID} недоступен.");
                    return;
                }
                if (!CheckCorrectPathConditions(job))
                {
                    Debug.LogWarning($"[Progression] Условия правильного пути для {job.jobID} не выполнены.");
                    return;
                }
            }
            else if (path == ProjectDocumentDefinition.PathType.Alternate)
            {
                if (job.alternatePath == null || !job.alternatePath.isValid)
                {
                    Debug.LogWarning($"[Progression] Обходной путь для {job.jobID} недоступен.");
                    return;
                }
                if (PlayerWallet.Instance == null ||
                    PlayerWallet.Instance.GetCurrentMoney() < job.alternatePath.moneyCost)
                {
                    Debug.LogWarning($"[Progression] Недостаточно денег для обходного пути {job.jobID}");
                    return;
                }
            }
            else
            {
                Debug.LogWarning($"[Progression] Неизвестный путь для {job.jobID}: {path}");
                return;
            }

            // Создаём документ
            var docDef = new ProjectDocumentDefinition(job);
            docDef.jobPathType = path;
            docDef.jobMoneyCost = (path == ProjectDocumentDefinition.PathType.Alternate) ? job.alternatePath.moneyCost : 0;
            docDef.jobCorruptionCost = (path == ProjectDocumentDefinition.PathType.Alternate) ? job.alternatePath.corruptionCost : 0;

            // Регистрация (защита от дублей)
            if (DocumentManager.Instance != null)
                DocumentManager.Instance.RegisterActiveProjectDoc(job.jobID);

            // Укладка на стол директора
            var directorInbox = PolicyManager.Instance != null ? PolicyManager.Instance.directorInboxStack : null;
            if (directorInbox == null)
            {
                // Fallback: ищем DocumentStack с именем DirectorInbox в сцене
                var stacks = FindObjectsByType<DocumentStack>(FindObjectsSortMode.None);
                foreach (var s in stacks)
                {
                    if (s.gameObject.name.IndexOf("Director", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        directorInbox = s;
                        break;
                    }
                }
            }

            if (directorInbox != null)
            {
                // Ищем префаб документа в сцене/проекте — берём любой существующий ProjectDocumentObject как шаблон
                var docPrefab = FindJobDocPrefab();
                directorInbox.AddProjectDocument(docDef, docPrefab);
                Debug.Log($"[Progression] Документ повышения создан для {job.jobID} (путь: {path})");
            }
            else
            {
                Debug.LogError("[Progression] Не найден directorInboxStack для укладки документа!");
            }
        }

        /// <summary>
        /// Проверка условий правильного пути для должности.
        /// Вызывается из StartJobPromotion перед созданием документа.
        /// </summary>
        private bool CheckCorrectPathConditions(JobTitleData job)
        {
            if (job.correctPath == null || !job.correctPath.isValid) return false;
            var p = job.correctPath;

            // Дни
            if (p.requiredDays > 0 && TimeManager.Instance != null &&
                TimeManager.Instance.GetCurrentDay() < p.requiredDays)
                return false;

            // Клиенты
            if (p.requiredClients > 0 && ClientPathfinding.clientsExitedProcessed < p.requiredClients)
                return false;

            // Сотрудники
            if (p.requiredStaff > 0 && HiringManager.Instance != null &&
                HiringManager.Instance.AllStaff.Count < p.requiredStaff)
                return false;

            // Регионы
            if (p.requiredRegions > 0 && GetCapturedRegionsCount() < p.requiredRegions)
                return false;

            // Апгрейды
            if (p.requiredUpgrades > 0 && UpgradeManager.Instance != null &&
                UpgradeManager.Instance.GetPurchasedUpgradeNamesForSave().Count < p.requiredUpgrades)
                return false;

            // Деньги
            if (p.requiredMoney > 0 && PlayerWallet.Instance != null &&
                PlayerWallet.Instance.GetCurrentMoney() < p.requiredMoney)
                return false;

            return true;
        }

        /// <summary>
        /// Найти префаб ProjectDocumentObject для создания документа о должности.
        /// Берём первый попавшийся экземпляр/префаб в сцене.
        /// </summary>
        private GameObject FindJobDocPrefab()
        {
            // 1. Ищем любой существующий ProjectDocumentObject в сцене — используем как шаблон
            var existing = FindFirstObjectByType<Gameplay.Documents.ProjectDocumentObject>();
            if (existing != null) return existing.gameObject;
            return null;
        }

        #endregion

        #region Document Processing Hook

        /// <summary>
        /// Главный метод-хаб. Вызывается Архивариусом (или Системой), 
        /// когда "Проектный Документ" попадает в финальную точку (Архив).
        /// </summary>
        public void ProcessCompletedDocument(ProjectDocumentDefinition doc)
        {
            if (doc == null) return;

            switch (doc.docType)
            {
                case ProjectDocumentType.RegionUnlock:
                    FinalizeRegionUnlock(doc.targetRegion);
                    if (DocumentManager.Instance != null) DocumentManager.Instance.UnregisterActiveProjectDoc(doc.targetRegion.regionID);
                    break;
                case ProjectDocumentType.JobPromotion:
                    // Для alternate пути — списать деньги и начислить коррупцию при архивации
                    if (doc.jobPathType == ProjectDocumentDefinition.PathType.Alternate && doc.targetJob != null)
                    {
                        if (PlayerWallet.Instance != null && doc.jobMoneyCost > 0)
                        {
                            PlayerWallet.Instance.AddMoney(-doc.jobMoneyCost,
                                $"Должность (обходной путь): {doc.targetJob.titleName}",
                                IncomeType.Shadow);
                        }
                        if (doc.jobCorruptionCost > 0 && FinancialLedgerManager.Instance != null)
                        {
                            // Начисляем corruption напрямую в globalCorruptionScore
                            FinancialLedgerManager.Instance.globalCorruptionScore += doc.jobCorruptionCost;
                            Debug.Log($"[Progression] +{doc.jobCorruptionCost}% коррупции (обходной путь: {doc.targetJob.titleName})");
                        }
                    }
                    FinalizeJobPromotion(doc.targetJob);
                    if (DocumentManager.Instance != null) DocumentManager.Instance.UnregisterActiveProjectDoc(doc.targetJob.jobID);
                    break;
                case ProjectDocumentType.FacilityUpgrade:
                    UpgradeManager.Instance.ActivateUpgradeByID(doc.targetUpgradeID);
                    if (DocumentManager.Instance != null) DocumentManager.Instance.UnregisterActiveProjectDoc(doc.targetUpgradeID);
                    break;
                case ProjectDocumentType.Policy:
                    if (PolicyManager.Instance != null) PolicyManager.Instance.ActivatePolicy(doc.targetUpgradeID);
                    if (DocumentManager.Instance != null) DocumentManager.Instance.UnregisterActiveProjectDoc(doc.targetUpgradeID);
                    break;
            }
        }

        #endregion

        // --- Методы для сброса (New Game) ---
        public void ResetState()
        {
            currentInfluence = 0;
            unlockedRegionIDs.Clear();
            unlockedJobIDs.Clear();
            regionRuntimeStates.Clear();

            // Автоматически открываем стартовую должность, если она есть
            if (allJobsDatabase.Count > 0)
            {
                // Открываем стартовую должность (Директор)
                unlockedJobIDs.Add(allJobsDatabase[0].jobID);
            }

            // Пересоздаём runtime states для открытых регионов
            InitializeRegionStates();

            OnInfluenceChanged?.Invoke(currentInfluence);
            OnProgressionUpdated?.Invoke();
        }

        private void OnDestroy()
        {
            // Отписываемся от событий
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnDayChanged -= OnDayChanged;
            }
        }
    }
}
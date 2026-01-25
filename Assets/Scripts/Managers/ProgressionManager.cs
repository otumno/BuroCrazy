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
        
        // События для UI
        public event System.Action<int> OnInfluenceChanged;
        public event System.Action OnProgressionUpdated; // Срабатывает при любом открытии региона/должности

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // DontDestroyOnLoad не нужен, так как родительский объект [SYSTEMS] уже помечен как DontDestroyOnLoad в SystemsBootstrapper.
            }
            else
            {
                Destroy(gameObject);
            }
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

        public int GetCapturedRegionsCount() => unlockedRegionIDs.Count;

        // Этот метод будет вызываться, когда документ о захвате успешно обработан
        public void FinalizeRegionUnlock(RegionData region)
        {
            if (region == null) return;
            if (IsRegionUnlocked(region.regionID)) return;

            unlockedRegionIDs.Add(region.regionID);
            Debug.Log($"<color=green>[Progression] РЕГИОН ЗАХВАЧЕН: {region.displayName}</color>");
            
            OnProgressionUpdated?.Invoke();
            
            // Здесь в будущем можно вызвать NotificationUI
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

            unlockedJobIDs.Add(job.jobID);
            Debug.Log($"<color=magenta>[Progression] ПОВЫШЕНИЕ ПОЛУЧЕНО: {job.titleName}</color>");

            if (job.isMinisterPosition)
            {
                Debug.Log("!!! ПОБЕДА !!! Игрок достиг ранга Министр.");
                // TODO: TriggerWinSequence();
            }

            OnProgressionUpdated?.Invoke();
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
                    break;
                case ProjectDocumentType.JobPromotion:
                    FinalizeJobPromotion(doc.targetJob);
                    break;
                case ProjectDocumentType.FacilityUpgrade:
                    // --- ИЗМЕНЕНИЕ: Теперь метод существует ---
                    UpgradeManager.Instance.ActivateUpgradeByID(doc.targetUpgradeID);
                    // ------------------------------------------
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
            
            // Автоматически открываем стартовый регион и должность, если они есть
            if (allRegionsDatabase.Count > 0)
            {
                // Для теста открываем первый регион (обычно Трущобы)
                unlockedRegionIDs.Add(allRegionsDatabase[0].regionID);
            }
            if (allJobsDatabase.Count > 0)
            {
                // Открываем стартовую должность (Директор)
                unlockedJobIDs.Add(allJobsDatabase[0].jobID);
            }
            
            OnInfluenceChanged?.Invoke(currentInfluence);
            OnProgressionUpdated?.Invoke();
        }
        
        // TODO: Добавить методы Save/Load, интегрируемые в SaveLoadManager
    }
}
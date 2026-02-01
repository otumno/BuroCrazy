// Assets/Scripts/UI/Map/MapPanelUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Scriptables.Progression;
using Data.Documents;
using Managers;
using Gameplay.Documents; 

namespace UI.Map
{
    public class MapPanelUI : MonoBehaviour
    {
        [Header("Общее управление")]
        [SerializeField] private Button closeButton;

        [Header("--- ЗОНА КАРТЫ (ЛЕВАЯ) ---")]
        [SerializeField] private GameObject regionInfoPanel; // Панель с деталями региона
        [SerializeField] private TextMeshProUGUI r_Title;
        [SerializeField] private TextMeshProUGUI r_Desc;
        [SerializeField] private TextMeshProUGUI r_Cost;
        [SerializeField] private Button r_ActionButton; 
        [SerializeField] private TextMeshProUGUI r_ButtonText;
        [SerializeField] private GameObject regionDocPrefab; // Красная папка

        [Header("--- ЗОНА КАРЬЕРЫ (ПРАВАЯ) ---")]
        [SerializeField] private GameObject jobInfoPanel;    // Панель с деталями должности
        [SerializeField] private TextMeshProUGUI j_Title;
        [SerializeField] private TextMeshProUGUI j_Desc;
        [SerializeField] private TextMeshProUGUI j_Cost;
        [SerializeField] private Button j_ActionButton;
        [SerializeField] private TextMeshProUGUI j_ButtonText;
        [SerializeField] private GameObject jobDocPrefab;    // Синяя папка

        [Header("Связи со сценой")]
        [Tooltip("Перетащите сюда стопку 'Входящие' со стола Директора")]
        [SerializeField] private DocumentStack directorInboxStack;

        [Header("Списки кнопок (Назначить вручную!)")]
        [SerializeField] private List<RegionSlotUI> regionSlots;
        [SerializeField] private List<JobNodeUI> jobNodes;

        private RegionData selectedRegion;
        private JobTitleData selectedJob;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
        }

        private void OnEnable()
        {
            Debug.Log("[MapPanelUI] OnEnable called");
            MainUIManager.Instance?.PushPause();
            RefreshAllButtons();

            // Скрываем инфо-панели при открытии, чтобы не висела старая инфа
            if(regionInfoPanel) 
            {
                Debug.Log("[MapPanelUI] regionInfoPanel exists, setting to false");
                regionInfoPanel.SetActive(false);
            }
            else
            {
                Debug.LogWarning("[MapPanelUI] regionInfoPanel is NULL!");
            }
            
            if(jobInfoPanel) jobInfoPanel.SetActive(false);

            // Показываем информацию о первом регионе если есть
            if (regionSlots != null && regionSlots.Count > 0)
            {
                var firstSlot = regionSlots.Find(s => s != null && s.regionData != null);
                if (firstSlot != null)
                {
                    Debug.Log($"[MapPanelUI] Showing info for first region: {firstSlot.regionData.displayName}");
                    ShowRegionInfo(firstSlot.regionData);
                }
            }
            else
            {
                Debug.LogWarning("[MapPanelUI] regionSlots is null or empty!");
            }
        }

        public void RefreshAllButtons()
        {
            if (ProgressionManager.Instance == null)
            {
                Debug.LogWarning("[MapPanelUI] ProgressionManager.Instance is null! Check if manager exists on scene.");
                return;
            }

            foreach (var slot in regionSlots)
            {
                if (slot != null) slot.UpdateState();
            }
            foreach (var node in jobNodes)
            {
                if (node != null) node.UpdateState();
            }
        }

        // =================================================================================
        // ЛОГИКА РЕГИОНОВ
        // =================================================================================

        public void ShowRegionInfo(RegionData region)
        {
            Debug.Log($"[MapPanelUI] ShowRegionInfo called with region: {region?.regionID ?? "NULL"}");

            selectedRegion = region;
            if (selectedRegion == null)
            {
                Debug.LogWarning("[MapPanelUI] selectedRegion is null, returning early.");
                return;
            }

            // Проверка что панель существует
            if (regionInfoPanel == null)
            {
                Debug.LogError("[MapPanelUI] regionInfoPanel is NOT assigned in Inspector! This is why no info appears.");
                return;
            }

            // Проверяем что панель включена
            bool wasActive = regionInfoPanel.activeSelf;
            Debug.Log($"[MapPanelUI] regionInfoPanel was active: {wasActive}");

            Debug.Log($"[MapPanelUI] UI Elements Check:");
            Debug.Log($"  r_Title: {(r_Title != null ? "assigned" : "NULL!")}");
            Debug.Log($"  r_Desc: {(r_Desc != null ? "assigned" : "NULL!")}");
            Debug.Log($"  r_Cost: {(r_Cost != null ? "assigned" : "NULL!")}");
            Debug.Log($"  r_ActionButton: {(r_ActionButton != null ? "assigned" : "NULL!")}");

            // Проверяем текст ДО изменения
            if (r_Title != null)
            {
                Debug.Log($"[MapPanelUI] r_Title text BEFORE: '{r_Title.text}'");
            }
            if (r_Desc != null)
            {
                Debug.Log($"[MapPanelUI] r_Desc text BEFORE: '{r_Desc.text}'");
            }

            Debug.Log($"[MapPanelUI] Activating regionInfoPanel for {selectedRegion.displayName}");
            regionInfoPanel.SetActive(true);

            // Force TMP to rebuild immediately
            if (regionInfoPanel.TryGetComponent<RectTransform>(out var rect))
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }

            // Всегда обновляем текст, даже если панель была активна
            if (r_Title != null)
            {
                r_Title.text = region.displayName ?? "Unknown";
                r_Title.ForceMeshUpdate();
                Debug.Log($"[MapPanelUI] r_Title AFTER: '{r_Title.text}'");
            }

            if (ProgressionManager.Instance != null)
            {
                var runtimeState = ProgressionManager.Instance.GetRegionRuntimeState(region);

                if (runtimeState != null && runtimeState.isUnlocked)
                {
                    int currentFlow = runtimeState.currentDailyTarget;
                    int maxFlow = region.maxDailyFlow;
                    float progress = runtimeState.GetFlowPercentage();

                    r_Title.text = region.displayName ?? "Unknown";
                    r_Title.ForceMeshUpdate();

                    string archetypeInfo = "";
                    if (region.groupWeights != null && region.groupWeights.Count > 0)
                    {
                        archetypeInfo = $"\n<color=magenta>👥 Типы посетителей:</color>";
                        foreach (var gw in region.groupWeights)
                        {
                            int groupFlow = Mathf.RoundToInt(currentFlow * gw.weight);
                            archetypeInfo += $"\n  • {gw.groupID}: ~{groupFlow} чел ({gw.weight * 100:F0}%)";
                        }
                    }

                    string progressInfo = "";
                    if (!runtimeState.IsAtFullCapacity())
                    {
                        int daysLeft = region.rampUpDays - runtimeState.daysOwned;
                        progressInfo = $"\n<color=green>⏱️ До полного потока: {daysLeft} дн.</color>";
                    }
                    else
                    {
                        progressInfo = $"\n<color=green>✅ Полный поток достигнут!</color>";
                    }

                    string flowInfo = $"\n\n<color=cyan>📊 Поток клиентов:</color>\n" +
                        $"  Текущий: <color=yellow>{currentFlow}</color> чел/день\n" +
                        $"  Максимум: {maxFlow} чел/день\n" +
                        $"  Прогресс: {progress * 100:F0}%";

                    string fullDescription = $"{region.description}{flowInfo}{archetypeInfo}{progressInfo}";

                    if (r_Desc != null)
                    {
                        r_Desc.text = fullDescription;
                        r_Desc.ForceMeshUpdate();
                        Debug.Log($"[MapPanelUI] r_Desc AFTER: '{r_Desc.text.Substring(0, Mathf.Min(50, r_Desc.text.Length))}...'");
                    }
                }
            }

            if (r_Cost != null)
            {
                r_Cost.ForceMeshUpdate();
                Debug.Log($"[MapPanelUI] r_Cost AFTER: '{r_Cost.text}'");
            }

            if (r_ActionButton != null)
            {
                // Update button state
                bool isUnlocked = ProgressionManager.Instance != null && ProgressionManager.Instance.IsRegionUnlocked(region.regionID);
                bool isPending = DocumentManager.Instance != null && DocumentManager.Instance.IsProjectDocPending(region.regionID);

                r_ActionButton.interactable = false;

                if (isUnlocked)
                {
                    r_ActionButton.interactable = false;
                    if (r_ButtonText != null) r_ButtonText.text = "Собственность";
                }
                else if (isPending)
                {
                    r_ActionButton.interactable = false;
                    if (r_ButtonText != null) r_ButtonText.text = "В пути";
                }
                else
                {
                    bool enoughMoney = PlayerWallet.Instance != null && PlayerWallet.Instance.GetCurrentMoney() >= region.unlockCostMoney;
                    bool enoughInf = ProgressionManager.Instance != null && ProgressionManager.Instance.GetInfluence() >= region.unlockCostInfluence;

                    r_ActionButton.interactable = enoughMoney && enoughInf;
                    if (r_ButtonText != null) r_ButtonText.text = "Подготовить приказ";
                }

                r_ActionButton.ForceUpdateLayout();
            }

            Debug.Log($"[MapPanelUI] ShowRegionInfo COMPLETED for {region.displayName}");
        }

            // Проверка что панель существует
            if (regionInfoPanel == null)
            {
                Debug.LogError("[MapPanelUI] regionInfoPanel is NOT assigned in Inspector! This is why no info appears.");
                return;
            }

            // Логи для диагностики UI элементов
            Debug.Log($"[MapPanelUI] UI Elements Check:");
            Debug.Log($"  r_Title: {(r_Title != null ? "assigned" : "NULL!")}");
            Debug.Log($"  r_Desc: {(r_Desc != null ? "assigned" : "NULL!")}");
            Debug.Log($"  r_Cost: {(r_Cost != null ? "assigned" : "NULL!")}");
            Debug.Log($"  r_ActionButton: {(r_ActionButton != null ? "assigned" : "NULL!")}");

            // Проверяем текст ДО изменения
            if (r_Title != null)
            {
                Debug.Log($"[MapPanelUI] r_Title text BEFORE: '{r_Title.text}'");
            }
            if (r_Desc != null)
            {
                Debug.Log($"[MapPanelUI] r_Desc text BEFORE: '{r_Desc.text}'");
            }

            Debug.Log($"[MapPanelUI] Activating regionInfoPanel for {selectedRegion.displayName}");
            regionInfoPanel.SetActive(true);

            // Force TMP to rebuild immediately
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(regionInfoPanel.GetComponent<RectTransform>());

            if (r_Title != null)
            {
                r_Title.text = region.displayName ?? "Unknown";
                r_Title.ForceMeshUpdate();
                Debug.Log($"[MapPanelUI] r_Title AFTER: '{r_Title.text}'");
            }
            else Debug.LogWarning("[MapPanelUI] r_Title is NULL - cannot set title!");

            // Получаем информацию о потоке если регион открыт
            string flowInfo = "";
            string archetypeInfo = "";
            string progressInfo = "";

            if (ProgressionManager.Instance != null)
            {
                var runtimeState = ProgressionManager.Instance.GetRegionRuntimeState(region);

                if (runtimeState != null && runtimeState.isUnlocked)
                {
                    // Информация о потоке
                    int currentFlow = runtimeState.currentDailyTarget;
                    int maxFlow = region.maxDailyFlow;
                    float progress = runtimeState.GetFlowPercentage();

                    flowInfo = $"\n\n<color=cyan>📊 Поток клиентов:</color>\n" +
                               $"  Текущий: <color=yellow>{currentFlow}</color> чел/день\n" +
                               $"  Максимум: {maxFlow} чел/день\n" +
                               $"  Прогресс: {progress * 100:F0}%";

                    // Информация об архетипах
                    if (region.groupWeights != null && region.groupWeights.Count > 0)
                    {
                        archetypeInfo = $"\n<color=magenta>👥 Типы посетителей:</color>";
                        foreach (var gw in region.groupWeights)
                        {
                            int groupFlow = Mathf.RoundToInt(currentFlow * gw.weight);
                            archetypeInfo += $"\n  • {gw.groupID}: ~{groupFlow} чел ({gw.weight * 100:F0}%)";
                        }
                    }

                    // Прогресс разогрева
                    if (!runtimeState.IsAtFullCapacity())
                    {
                        int daysLeft = region.rampUpDays - runtimeState.daysOwned;
                        progressInfo = $"\n<color=green>⏱️ До полного потока: {daysLeft} дн.</color>";
                    }
                    else
                    {
                        progressInfo = $"\n<color=green>✅ Полный поток достигнут!</color>";
                    }
                }
                else if (region.maxDailyFlow > 0)
                {
                    // Регион закрыт, но показываем потенциал
                    flowInfo = $"\n\n<color=cyan>📊 Потенциал:</color>\n" +
                               $"  Максимум: {region.maxDailyFlow} чел/день\n" +
                               $"  Прогрев: {region.rampUpDays} дней";

                    if (region.groupWeights != null && region.groupWeights.Count > 0)
                    {
                        archetypeInfo = $"\n<color=magenta>👥 Типы посетителей:</color>";
                        foreach (var gw in region.groupWeights)
                        {
                            archetypeInfo += $"\n  • {gw.groupID}: {gw.weight * 100:F0}%";
                        }
                    }
                }
            }

            // Формируем полное описание
            string fullDescription = $"{region.description}{flowInfo}{archetypeInfo}{progressInfo}";

            if (r_Desc != null)
            {
                r_Desc.text = fullDescription;
                r_Desc.ForceMeshUpdate();
                Debug.Log($"[MapPanelUI] r_Desc AFTER: '{r_Desc.text.Substring(0, Mathf.Min(50, r_Desc.text.Length))}...'");
            }

            if (r_Cost != null)
            {
                r_Cost.ForceMeshUpdate();
                Debug.Log($"[MapPanelUI] r_Cost AFTER: '{r_Cost.text}'");
            }

            if (ProgressionManager.Instance == null)
            {
                Debug.LogError("[MapPanelUI] ProgressionManager.Instance is null!");
                return;
            }

            bool isUnlocked = ProgressionManager.Instance.IsRegionUnlocked(region.regionID);
            
            // --- ПРОВЕРКА НА АКТИВНЫЙ ПРИКАЗ ---
            bool isPending = DocumentManager.Instance != null && DocumentManager.Instance.IsProjectDocPending(region.regionID);

            if (r_Cost != null)
            {
                if (isUnlocked)
                {
                    r_Cost.text = "<color=green>ТЕРРИТОРИЯ ПОД КОНТРОЛЕМ</color>";
                }
                else if (isPending)
                {
                    r_Cost.text = "<color=yellow>ОФОРМЛЕНИЕ ДОКУМЕНТОВ...</color>";
                }
                else
                {
                    r_Cost.text = $"Бюджет: ${region.unlockCostMoney}\nВлияние: {region.unlockCostInfluence}";
                }
            }

            if (r_ActionButton != null)
            {
                r_ActionButton.interactable = false; // Сначала блокируем

                if (isUnlocked)
                {
                    r_ActionButton.interactable = false;
                    if (r_ButtonText != null) r_ButtonText.text = "Собственность";
                }
                else if (isPending)
                {
                    r_ActionButton.interactable = false;
                    if (r_ButtonText != null) r_ButtonText.text = "В пути";
                }
                else
                {
                    // Проверка ресурсов
                    bool enoughMoney = PlayerWallet.Instance != null && PlayerWallet.Instance.GetCurrentMoney() >= region.unlockCostMoney;
                    bool enoughInf = ProgressionManager.Instance.GetInfluence() >= region.unlockCostInfluence;

                    r_ActionButton.interactable = enoughMoney && enoughInf;
                    
                    if (r_ButtonText != null) r_ButtonText.text = "Подготовить приказ";
                    
                    r_ActionButton.onClick.RemoveAllListeners();
                    r_ActionButton.onClick.AddListener(SpawnRegionDocument);
                }
            }
        }

        private void SpawnRegionDocument()
        {
            if (selectedRegion == null)
            {
                Debug.LogWarning("[MapPanelUI] selectedRegion is null!");
                return;
            }

            if (directorInboxStack == null)
            {
                Debug.LogError("[MapPanelUI] directorInboxStack is not assigned! Assign 'Incoming Documents Stack' from Director Desk.");
                return;
            }

            // Создаем и передаем документ
            var docData = new ProjectDocumentDefinition(selectedRegion);
            directorInboxStack.AddProjectDocument(docData, regionDocPrefab);

            ClosePanel();
            Debug.Log($"Документ на захват '{selectedRegion.displayName}' отправлен директору.");
        }

        // =================================================================================
        // ЛОГИКА КАРЬЕРЫ
        // =================================================================================

        public void ShowJobInfo(JobTitleData job)
        {
            Debug.Log($"[MapPanelUI] ShowJobInfo called with job: {job?.jobID ?? "NULL"}");

            selectedJob = job;
            if (selectedJob == null)
            {
                Debug.LogWarning("[MapPanelUI] selectedJob is null, returning early.");
                return;
            }

            if (jobInfoPanel == null)
            {
                Debug.LogError("[MapPanelUI] jobInfoPanel is NOT assigned in Inspector! This is why no info appears.");
                return;
            }

            Debug.Log($"[MapPanelUI] Activating jobInfoPanel for {selectedJob.titleName}");
            jobInfoPanel.SetActive(true);

            if (j_Title != null) j_Title.text = job.titleName ?? "Unknown";

            if (ProgressionManager.Instance == null)
            {
                Debug.LogError("[MapPanelUI] ProgressionManager.Instance is null!");
                return;
            }

            int currentRegions = ProgressionManager.Instance.GetCapturedRegionsCount();
            int requiredRegions = job.requiredCapturedRegionsCount;
            string regionColor = currentRegions >= requiredRegions ? "green" : "red";

            string reqText = $"Требуется регионов: <color={regionColor}>{currentRegions} / {requiredRegions}</color>";
            
            if (j_Desc != null)
                j_Desc.text = $"{job.description}\n\n{reqText}";

            bool isUnlocked = ProgressionManager.Instance.IsJobUnlocked(job.jobID);
            bool canStart = ProgressionManager.Instance.CanStartUnlockJob(job);
            
            // --- ПРОВЕРКА НА АКТИВНЫЙ ПРИКАЗ ---
            bool isPending = DocumentManager.Instance != null && DocumentManager.Instance.IsProjectDocPending(job.jobID);

            if (j_Cost != null)
            {
                if (isUnlocked)
                {
                    j_Cost.text = "<color=green>ТЕКУЩАЯ ДОЛЖНОСТЬ</color>";
                }
                else if (isPending)
                {
                    j_Cost.text = "<color=yellow>РАССМОТРЕНИЕ...</color>";
                }
                else
                {
                    j_Cost.text = $"Взнос: ${job.costMoney}\nВлияние: {job.costInfluence}";
                }
            }

            if (j_ActionButton != null)
            {
                j_ActionButton.interactable = false;

                if (isUnlocked)
                {
                    j_ActionButton.interactable = false;
                    if (j_ButtonText != null) j_ButtonText.text = "Получено";
                }
                else if (isPending)
                {
                    j_ActionButton.interactable = false;
                    if (j_ButtonText != null) j_ButtonText.text = "Ждите";
                }
                else
                {
                    bool enoughMoney = PlayerWallet.Instance != null && PlayerWallet.Instance.GetCurrentMoney() >= job.costMoney;
                    bool enoughInf = ProgressionManager.Instance.GetInfluence() >= job.costInfluence;

                    j_ActionButton.interactable = canStart && enoughMoney && enoughInf;
                    
                    if (j_ButtonText != null)
                    {
                        if (!canStart) j_ButtonText.text = "Недоступно";
                        else j_ButtonText.text = "Подать прошение";
                    }
                    
                    j_ActionButton.onClick.RemoveAllListeners();
                    j_ActionButton.onClick.AddListener(SpawnJobDocument);
                }
            }
        }

        private void SpawnJobDocument()
        {
            if (selectedJob == null)
            {
                Debug.LogWarning("[MapPanelUI] selectedJob is null!");
                return;
            }

            if (directorInboxStack == null)
            {
                Debug.LogError("[MapPanelUI] directorInboxStack is not assigned! Assign 'Incoming Documents Stack' from Director Desk.");
                return;
            }

            // Создаем и передаем документ
            var docData = new ProjectDocumentDefinition(selectedJob);
            directorInboxStack.AddProjectDocument(docData, jobDocPrefab);

            ClosePanel();
            Debug.Log($"Прошение на должность '{selectedJob.titleName}' отправлено директору.");
        }

        private void ClosePanel()
        {
            gameObject.SetActive(false);
            if (MainUIManager.Instance != null) MainUIManager.Instance.PopPause();
        }

        // Для отладки - вызывать из консоли
        [ContextMenu("Debug: Test Map Panel")]
        public void DebugTestMapPanel()
        {
            Debug.Log("========================================");
            Debug.Log("=== MapPanelUI Debug Test (DETAILED) ===");
            Debug.Log("========================================");
            Debug.Log($"regionInfoPanel assigned: {regionInfoPanel != null}");
            Debug.Log($"jobInfoPanel assigned: {jobInfoPanel != null}");
            Debug.Log($"ProgressionManager.Instance: {ProgressionManager.Instance != null}");
            Debug.Log($"PlayerWallet.Instance: {PlayerWallet.Instance != null}");
            Debug.Log($"DocumentManager.Instance: {DocumentManager.Instance != null}");
            Debug.Log($"directorInboxStack: {directorInboxStack != null}");
            Debug.Log($"closeButton: {closeButton != null}");
            Debug.Log($"r_Title: {r_Title != null}");
            Debug.Log($"r_Desc: {r_Desc != null}");
            Debug.Log($"r_Cost: {r_Cost != null}");
            Debug.Log($"r_ActionButton: {r_ActionButton != null}");
            Debug.Log($"j_Title: {j_Title != null}");
            Debug.Log($"j_Desc: {j_Desc != null}");
            Debug.Log($"j_Cost: {j_Cost != null}");
            Debug.Log($"j_ActionButton: {j_ActionButton != null}");
            Debug.Log($"regionSlots count: {regionSlots?.Count ?? 0}");
            Debug.Log($"jobNodes count: {jobNodes?.Count ?? 0}");

            if (regionInfoPanel != null)
                Debug.Log($"regionInfoPanel.activeSelf: {regionInfoPanel.activeSelf}");

            if (jobInfoPanel != null)
                Debug.Log($"jobInfoPanel.activeSelf: {jobInfoPanel.activeSelf}");

            if (ProgressionManager.Instance != null)
            {
                int captured = ProgressionManager.Instance.GetCapturedRegionsCount();
                int influence = ProgressionManager.Instance.GetInfluence();
                Debug.Log($"Captured regions: {captured}, Influence: {influence}");
            }
            Debug.Log("========================================");
        }

        // Дополнительный тест - проверить все RegionSlotUI
        [ContextMenu("Debug: Test Region Slots")]
        public void DebugTestRegionSlots()
        {
            Debug.Log("=== Testing Region Slots ===");
            if (regionSlots == null || regionSlots.Count == 0)
            {
                Debug.LogWarning("regionSlots list is empty or null! Did you assign RegionSlotUI components in Inspector?");
                return;
            }

            for (int i = 0; i < regionSlots.Count; i++)
            {
                var slot = regionSlots[i];
                if (slot != null)
                {
                    Debug.Log($"RegionSlot[{i}]: {slot.regionData?.regionID ?? "NULL DATA"}");
                }
                else
                {
                    Debug.LogWarning($"RegionSlot[{i}]: NULL REFERENCE in list!");
                }
            }
        }

        // Дополнительный тест - проверить все JobNodeUI
        [ContextMenu("Debug: Test Job Nodes")]
        public void DebugTestJobNodes()
        {
            Debug.Log("=== Testing Job Nodes ===");
            if (jobNodes == null || jobNodes.Count == 0)
            {
                Debug.LogWarning("jobNodes list is empty or null! Did you assign JobNodeUI components in Inspector?");
                return;
            }

            for (int i = 0; i < jobNodes.Count; i++)
            {
                var node = jobNodes[i];
                if (node != null)
                {
                    Debug.Log($"JobNode[{i}]: {node.jobData?.jobID ?? "NULL DATA"}");
                }
                else
                {
                    Debug.LogWarning($"JobNode[{i}]: NULL REFERENCE in list!");
                }
            }
        }
    }
}
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
            MainUIManager.Instance?.PushPause();
            RefreshAllButtons();
            
            // Скрываем инфо-панели при открытии, чтобы не висела старая инфа
            if(regionInfoPanel) regionInfoPanel.SetActive(false);
            if(jobInfoPanel) jobInfoPanel.SetActive(false);
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

            Debug.Log($"[MapPanelUI] Activating regionInfoPanel for {selectedRegion.displayName}");
            regionInfoPanel.SetActive(true);

            if (r_Title != null) r_Title.text = region.displayName ?? "Unknown";

            // Формируем описание бонусов
            string bonusText = "";
            if (region.spawnBonuses != null)
            {
                foreach(var b in region.spawnBonuses)
                    bonusText += $"\n • +{b.additionalClients} клиентов ({b.period})";
            }

            if (r_Desc != null)
                r_Desc.text = $"{region.description}\n\n<color=yellow>Бонусы:</color>{bonusText}";

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
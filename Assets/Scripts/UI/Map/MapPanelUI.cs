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
        [SerializeField] private GameObject regionInfoPanel;
        [SerializeField] private TextMeshProUGUI r_Title;
        [SerializeField] private TextMeshProUGUI r_Desc;
        [SerializeField] private TextMeshProUGUI r_Cost;
        [SerializeField] private Button r_ActionButton;
        [SerializeField] private TextMeshProUGUI r_ButtonText;
        [SerializeField] private GameObject regionDocPrefab;

        [Header("--- ЗОНА КАРЬЕРЫ (ПРАВАЯ) ---")]
        [SerializeField] private GameObject jobInfoPanel;
        [SerializeField] private TextMeshProUGUI j_Title;
        [SerializeField] private TextMeshProUGUI j_Desc;
        [SerializeField] private TextMeshProUGUI j_Cost;
        [SerializeField] private Button j_ActionButton;
        [SerializeField] private TextMeshProUGUI j_ButtonText;
        [SerializeField] private GameObject jobDocPrefab;

        [Header("Связи со сценой")]
        [SerializeField] private DocumentStack directorInboxStack;

        [Header("Списки кнопок")]
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

            if (regionInfoPanel) regionInfoPanel.SetActive(false);
            if (jobInfoPanel) jobInfoPanel.SetActive(false);

            if (regionSlots != null && regionSlots.Count > 0)
            {
                var firstSlot = regionSlots.Find(s => s != null && s.regionData != null);
                if (firstSlot != null)
                {
                    ShowRegionInfo(firstSlot.regionData);
                }
            }
        }

        public void RefreshAllButtons()
        {
            if (ProgressionManager.Instance == null) return;

            foreach (var slot in regionSlots)
            {
                if (slot != null) slot.UpdateState();
            }
            foreach (var node in jobNodes)
            {
                if (node != null) node.UpdateState();
            }
        }

        public void ShowRegionInfo(RegionData region)
        {
            Debug.Log($"[MapPanelUI] ShowRegionInfo called with region: {region?.regionID ?? "NULL"}");

            selectedRegion = region;
            if (selectedRegion == null)
            {
                Debug.LogWarning("[MapPanelUI] selectedRegion is null, returning early.");
                return;
            }

            if (regionInfoPanel == null)
            {
                Debug.LogError("[MapPanelUI] regionInfoPanel is NOT assigned in Inspector!");
                return;
            }

            if (r_Title != null)
            {
                Debug.Log($"[MapPanelUI] r_Title text BEFORE: '{r_Title.text}'");
            }

            Debug.Log($"[MapPanelUI] Activating regionInfoPanel for {selectedRegion.displayName}");
            regionInfoPanel.SetActive(true);

            if (r_Title != null)
            {
                r_Title.text = region.displayName ?? "Unknown";
                r_Title.ForceMeshUpdate();
                Debug.Log($"[MapPanelUI] r_Title AFTER: '{r_Title.text}'");
            }

            string flowInfo = "";
            string archetypeInfo = "";
            string progressInfo = "";

            if (ProgressionManager.Instance != null)
            {
                var runtimeState = ProgressionManager.Instance.GetRegionRuntimeState(region);

                if (runtimeState != null && runtimeState.isUnlocked)
                {
                    int currentFlow = runtimeState.currentDailyTarget;
                    int maxFlow = region.maxDailyFlow;
                    float progress = runtimeState.GetFlowPercentage();

                    flowInfo = $"\n\n<color=cyan>📊 Поток клиентов:</color>\n" +
                        $"  Текущий: <color=yellow>{currentFlow}</color> чел/день\n" +
                        $"  Максимум: {maxFlow} чел/день\n" +
                        $"  Прогресс: {progress * 100:F0}%";

                    if (region.groupWeights != null && region.groupWeights.Count > 0)
                    {
                        archetypeInfo = $"\n<color=magenta>👥 Типы посетителей:</color>";
                        foreach (var gw in region.groupWeights)
                        {
                            int groupFlow = Mathf.RoundToInt(currentFlow * gw.weight);
                            archetypeInfo += $"\n  • {gw.groupID}: ~{groupFlow} чел ({gw.weight * 100:F0}%)";
                        }
                    }

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

            string fullDescription = $"{region.description}{flowInfo}{archetypeInfo}{progressInfo}";

            if (r_Desc != null)
            {
                r_Desc.text = fullDescription;
                r_Desc.ForceMeshUpdate();
                Debug.Log($"[MapPanelUI] r_Desc AFTER: '{r_Desc.text.Substring(0, Mathf.Min(50, r_Desc.text.Length))}...'");
            }

            bool isUnlocked = false;
            bool isPending = false;

            if (ProgressionManager.Instance != null)
            {
                isUnlocked = ProgressionManager.Instance.IsRegionUnlocked(region.regionID);
            }

            if (DocumentManager.Instance != null)
            {
                isPending = DocumentManager.Instance.IsProjectDocPending(region.regionID);
            }

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
                r_Cost.ForceMeshUpdate();
                Debug.Log($"[MapPanelUI] r_Cost AFTER: '{r_Cost.text}'");
            }

            if (r_ActionButton != null)
            {
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

                    r_ActionButton.onClick.RemoveAllListeners();
                    r_ActionButton.onClick.AddListener(SpawnRegionDocument);
                }
            }

            Debug.Log($"[MapPanelUI] ShowRegionInfo COMPLETED for {region.displayName}");
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
                Debug.LogError("[MapPanelUI] directorInboxStack is not assigned!");
                return;
            }

            var docData = new ProjectDocumentDefinition(selectedRegion);
            directorInboxStack.AddProjectDocument(docData, regionDocPrefab);

            ClosePanel();
            Debug.Log($"Документ на захват '{selectedRegion.displayName}' отправлен директору.");
        }

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
                Debug.LogError("[MapPanelUI] jobInfoPanel is NOT assigned in Inspector!");
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
                Debug.LogError("[MapPanelUI] directorInboxStack is not assigned!");
                return;
            }

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
    }
}

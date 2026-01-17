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
            RefreshAllButtons();
            
            // Скрываем инфо-панели при открытии, чтобы не висела старая инфа
            if(regionInfoPanel) regionInfoPanel.SetActive(false);
            if(jobInfoPanel) jobInfoPanel.SetActive(false);
        }

        public void RefreshAllButtons()
        {
            foreach (var slot in regionSlots) slot.UpdateState();
            foreach (var node in jobNodes) node.UpdateState();
        }

        // =================================================================================
        // ЛОГИКА РЕГИОНОВ
        // =================================================================================

        public void ShowRegionInfo(RegionData region)
        {
            selectedRegion = region;
            if (selectedRegion == null) return;

            regionInfoPanel.SetActive(true);
            r_Title.text = region.displayName;

            // Формируем описание бонусов
            string bonusText = "";
            if (region.spawnBonuses != null)
            {
                foreach(var b in region.spawnBonuses)
                    bonusText += $"\n • +{b.additionalClients} клиентов ({b.period})";
            }

            r_Desc.text = $"{region.description}\n\n<color=yellow>Бонусы:</color>{bonusText}";

            bool isUnlocked = ProgressionManager.Instance.IsRegionUnlocked(region.regionID);
            
            // --- ПРОВЕРКА НА АКТИВНЫЙ ПРИКАЗ ---
            bool isPending = DocumentManager.Instance != null && DocumentManager.Instance.IsProjectDocPending(region.regionID);

            if (isUnlocked)
            {
                r_Cost.text = "<color=green>ТЕРРИТОРИЯ ПОД КОНТРОЛЕМ</color>";
                r_ActionButton.interactable = false;
                r_ButtonText.text = "Собственность";
            }
            else if (isPending)
            {
                r_Cost.text = "<color=yellow>ОФОРМЛЕНИЕ ДОКУМЕНТОВ...</color>";
                r_ActionButton.interactable = false;
                r_ButtonText.text = "В пути";
            }
            else
            {
                r_Cost.text = $"Бюджет: ${region.unlockCostMoney}\nВлияние: {region.unlockCostInfluence}";
                
                // Проверка ресурсов
                bool enoughMoney = PlayerWallet.Instance.GetCurrentMoney() >= region.unlockCostMoney;
                bool enoughInf = ProgressionManager.Instance.GetInfluence() >= region.unlockCostInfluence;

                r_ActionButton.interactable = enoughMoney && enoughInf;
                r_ButtonText.text = "Подготовить приказ";
                
                r_ActionButton.onClick.RemoveAllListeners();
                r_ActionButton.onClick.AddListener(SpawnRegionDocument);
            }
        }

        private void SpawnRegionDocument()
        {
            if (selectedRegion == null || directorInboxStack == null) return;

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
            selectedJob = job;
            if (selectedJob == null) return;

            jobInfoPanel.SetActive(true);
            j_Title.text = job.titleName;

            int currentRegions = ProgressionManager.Instance.GetCapturedRegionsCount();
            int requiredRegions = job.requiredCapturedRegionsCount;
            string regionColor = currentRegions >= requiredRegions ? "green" : "red";

            string reqText = $"Требуется регионов: <color={regionColor}>{currentRegions} / {requiredRegions}</color>";
            j_Desc.text = $"{job.description}\n\n{reqText}";

            bool isUnlocked = ProgressionManager.Instance.IsJobUnlocked(job.jobID);
            bool canStart = ProgressionManager.Instance.CanStartUnlockJob(job);
            
            // --- ПРОВЕРКА НА АКТИВНЫЙ ПРИКАЗ ---
            bool isPending = DocumentManager.Instance != null && DocumentManager.Instance.IsProjectDocPending(job.jobID);

            if (isUnlocked)
            {
                j_Cost.text = "<color=green>ТЕКУЩАЯ ДОЛЖНОСТЬ</color>";
                j_ActionButton.interactable = false;
                j_ButtonText.text = "Получено";
            }
            else if (isPending)
            {
                j_Cost.text = "<color=yellow>РАССМОТРЕНИЕ...</color>";
                j_ActionButton.interactable = false;
                j_ButtonText.text = "Ждите";
            }
            else
            {
                j_Cost.text = $"Взнос: ${job.costMoney}\nВлияние: {job.costInfluence}";
                
                bool enoughMoney = PlayerWallet.Instance.GetCurrentMoney() >= job.costMoney;
                bool enoughInf = ProgressionManager.Instance.GetInfluence() >= job.costInfluence;

                j_ActionButton.interactable = canStart && enoughMoney && enoughInf;
                
                if (!canStart) j_ButtonText.text = "Недоступно";
                else j_ButtonText.text = "Подать прошение";

                j_ActionButton.onClick.RemoveAllListeners();
                j_ActionButton.onClick.AddListener(SpawnJobDocument);
            }
        }

        private void SpawnJobDocument()
        {
            if (selectedJob == null || directorInboxStack == null) return;

            // Создаем и передаем документ
            var docData = new ProjectDocumentDefinition(selectedJob);
            directorInboxStack.AddProjectDocument(docData, jobDocPrefab);

            ClosePanel();
            Debug.Log($"Прошение на должность '{selectedJob.titleName}' отправлено директору.");
        }

        private void ClosePanel()
        {
            gameObject.SetActive(false);
            if (MainUIManager.Instance != null) MainUIManager.Instance.ResumeGame(); // Снимаем паузу
        }
    }
}
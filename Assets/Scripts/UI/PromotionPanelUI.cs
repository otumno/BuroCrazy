using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class PromotionPanelUI : MonoBehaviour
{
    public static PromotionPanelUI Instance { get; private set; }

    [Header("Ссылки на UI")]
    [SerializeField] private GameObject panelObject;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI employeeNameText;
    [SerializeField] private TextMeshProUGUI flavorText;
    [SerializeField] private TextMeshProUGUI targetTitleText;
    
    [Header("Динамические области")]
    [SerializeField] private TextMeshProUGUI promotionTargetText;
    [SerializeField] private GameObject assignmentOptionsContainer;
    [SerializeField] private TextMeshProUGUI selectedAssignmentText;

    [Header("Кнопки")]
    [SerializeField] private Button approveButton;
    [SerializeField] private Button rejectButton;

    [Header("Префабы")]
    [SerializeField] private GameObject optionButtonPrefab;

    private StaffController currentStaff;
    private RankData selectedPromotion;
    
    // Список бюрократических фраз
    private List<string> bureaucraticTexts = new List<string>
    {
        "Настоящим, ввиду производственной необходимости и согласно параграфу 7-БИС устава, постановляется:",
        "В соответствии с директивой №1337 о кадровых перестановках и в целях оптимизации рабочих процессов, приказываю:",
        "Рассмотрев личное дело и приняв во внимание неоценимый вклад в борьбу с энтропией, решено:",
        "Во исполнение приказа о повышении эффективности и по итогам квартальной аттестации, надлежит:"
    };

    private void Awake()
    {
        Instance = this;
        approveButton.onClick.AddListener(OnApprove);
        rejectButton.onClick.AddListener(Hide);
        panelObject.SetActive(false);
    }

    public void ShowForStaff(StaffController staff)
    {
        currentStaff = staff;
        if (currentStaff == null || currentStaff.currentRank == null) return;

        List<RankData> promotionOptions = currentStaff.currentRank.possiblePromotions
            .Where(p => staff.experiencePoints >= p.experienceRequired).ToList();

        if (promotionOptions.Count == 0) return;

        // Сброс состояния UI
        selectedPromotion = null;
        approveButton.interactable = false;
        selectedAssignmentText.gameObject.SetActive(false);

        // Заполнение общих полей
        employeeNameText.text = $"Сотрудник: {staff.characterName}";
        flavorText.text = bureaucraticTexts[Random.Range(0, bureaucraticTexts.Count)];

        // Режим "Повышение" (один вариант)
        if (promotionOptions.Count == 1)
        {
            selectedPromotion = promotionOptions[0];
            titleText.text = "ПРИКАЗ О ПОВЫШЕНИИ";
            targetTitleText.text = "Повысить до должности:";
            
            promotionTargetText.gameObject.SetActive(true);
            assignmentOptionsContainer.SetActive(false);

            promotionTargetText.text = selectedPromotion.rankName;
            
            // Кнопка "Утвердить" активна, если хватает денег
            approveButton.interactable = PlayerWallet.Instance.GetCurrentMoney() >= selectedPromotion.promotionCost;
        }
        // Режим "Назначение" (много вариантов)
        else
        {
            titleText.text = "ПРИКАЗ О НАЗНАЧЕНИИ";
            targetTitleText.text = "Назначить на должность:";

            promotionTargetText.gameObject.SetActive(false);
            assignmentOptionsContainer.SetActive(true);

            foreach (Transform child in assignmentOptionsContainer.transform) { Destroy(child.gameObject); }

            foreach (RankData option in promotionOptions)
            {
                GameObject buttonGO = Instantiate(optionButtonPrefab, assignmentOptionsContainer.transform);
                buttonGO.GetComponent<PromotionOptionButton>().Setup(option, this);
            }
        }
        
        panelObject.SetActive(true);
        Time.timeScale = 0f; // Ставим игру на паузу
    }

    // Этот метод вызывается кнопкой выбора
    public void OnOptionSelected(RankData selectedRank)
    {
        selectedPromotion = selectedRank;
        
        // Прячем кнопки выбора и показываем текст с результатом
        assignmentOptionsContainer.SetActive(false);
        selectedAssignmentText.gameObject.SetActive(true);
        selectedAssignmentText.text = selectedPromotion.rankName;

        // Активируем кнопку подтверждения
        approveButton.interactable = true;
    }

    private void OnApprove()
    {
        if (currentStaff != null && selectedPromotion != null)
        {
            HiringManager.Instance.PromoteStaff(currentStaff, selectedPromotion);
        }
        Hide();
        FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include)?.RefreshTeamList();
    }

    public void Hide()
    {
        panelObject.SetActive(false);
        Time.timeScale = 1f; // Снимаем игру с паузы
    }
}
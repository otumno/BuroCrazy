using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Managers;

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
    
    private int flavorIndex;
    private bool isPromotion;

    private void OnEnable() => UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged += RefreshLocale;
    private void OnDisable() => UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged -= RefreshLocale;

    private void RefreshLocale(UnityEngine.Localization.Locale locale)
    {
        if (currentStaff == null) return;
        employeeNameText.text = UI.GameText.Get("promotion.employee", currentStaff.characterName);
        flavorText.text = UI.GameText.Get("promotion.flavor." + flavorIndex);
        titleText.text = UI.GameText.Get(isPromotion ? "promotion.title" : "promotion.assignment");
        targetTitleText.text = UI.GameText.Get(isPromotion ? "promotion.to" : "promotion.assign_to");
        if (selectedPromotion != null)
            (isPromotion ? promotionTargetText : selectedAssignmentText).text = selectedPromotion.LocalizedName;
    }

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
        flavorIndex = Random.Range(0, 4);
        isPromotion = promotionOptions.Count == 1;

        // Режим "Повышение" (один вариант)
        if (promotionOptions.Count == 1)
        {
            selectedPromotion = promotionOptions[0];

            
            promotionTargetText.gameObject.SetActive(true);
            assignmentOptionsContainer.SetActive(false);

            promotionTargetText.text = selectedPromotion.LocalizedName;
            
            // Кнопка "Утвердить" активна, если хватает денег
            approveButton.interactable = PlayerWallet.Instance.GetCurrentMoney() >= selectedPromotion.promotionCost;
        }
        // Режим "Назначение" (много вариантов)
        else
        {


            promotionTargetText.gameObject.SetActive(false);
            assignmentOptionsContainer.SetActive(true);

            foreach (Transform child in assignmentOptionsContainer.transform) { Destroy(child.gameObject); }

            foreach (RankData option in promotionOptions)
            {
                GameObject buttonGO = Instantiate(optionButtonPrefab, assignmentOptionsContainer.transform);
                buttonGO.GetComponent<PromotionOptionButton>().Setup(option, this);
            }
        }
        
        RefreshLocale(null);
        panelObject.SetActive(true);
        
        var animator = panelObject.GetComponent<UIWindowAnimator>();
        if (animator != null) animator.Open();
        
        MainUIManager.Instance.PushPause();
    }

    // Этот метод вызывается кнопкой выбора
    public void OnOptionSelected(RankData selectedRank)
    {
        selectedPromotion = selectedRank;
        
        // Прячем кнопки выбора и показываем текст с результатом
        assignmentOptionsContainer.SetActive(false);
        selectedAssignmentText.gameObject.SetActive(true);
        selectedAssignmentText.text = selectedPromotion.LocalizedName;

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
        var animator = panelObject.GetComponent<UIWindowAnimator>();
        if (animator != null)
        {
            animator.Close();
        }
        else
        {
            panelObject.SetActive(false);
            MainUIManager.Instance.PopPause();
        }
    }
}
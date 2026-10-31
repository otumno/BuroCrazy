using System;
using UnityEngine;
using UnityEngine.UI;
using Managers;
using TMPro;

public class StaffSchedulePanelUI : MonoBehaviour
{
    [Header("Управление окном")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backgroundButton;
    [Tooltip("Кнопка «Далее» для планирования в конце дня. Необязательна: без неё дальше ведёт closeButton.")]
    [SerializeField] private Button nextButton;
    [Tooltip("Подсказка, что днём смены менять нельзя. Необязательна.")]
    [SerializeField] private TextMeshProUGUI lockedHintText;

    [Header("Часть 2: Заголовки периодов")]
    [SerializeField] private Transform periodHeaderContainer;
    [SerializeField] private GameObject periodTitlePrefab;

    [Header("Часть 3: Список имен")]
    [SerializeField] private Transform staffNamesContainer;
    [SerializeField] private GameObject staffNamePrefab; 

    [Header("Часть 4: Сетка ячеек")]
    [SerializeField] private Transform rowsContainer;
    [SerializeField] private GameObject scheduleRowPrefab; 

    [Header("Данные")]
    [SerializeField] private RoleColorDatabase roleColorDb;
    
    [Header("Связи")]
    [Tooltip("Ссылка на ActionConfigPopupUI на сцене (для открытия настроек сотрудника)")]
    [SerializeField] private ActionConfigPopupUI configPopup;

    /// <summary>
    /// Можно ли менять смены. Во время активного дня панель открывается только для просмотра (см. ShiftPlanning).
    /// </summary>
    public bool IsEditable { get; private set; }

    // Панель открыта как шаг ночной цепочки конца дня: закрытие ведёт к следующему экрану.
    private bool _isPlanningStep;

    public event Action Closed;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (backgroundButton != null) backgroundButton.onClick.AddListener(BackgroundButton_Clicked);
        if (nextButton != null) nextButton.onClick.AddListener(Hide);
    }

    /// <summary>
    /// Расписание из окна персонала: редактируется, только пока день не начат.
    /// </summary>
    public void Show()
    {
        SetMode(ShiftPlanning.IsOpen, false);
        Open();
    }

    /// <summary>
    /// Экран планирования смен на следующий день (цепочка конца дня).
    /// </summary>
    public void ShowPlanning()
    {
        SetMode(true, true);
        Open();
    }

    private void SetMode(bool isEditable, bool isPlanningStep)
    {
        IsEditable = isEditable;
        _isPlanningStep = isPlanningStep;

        if (lockedHintText != null) lockedHintText.gameObject.SetActive(!isEditable);

        if (nextButton == null) return;
        nextButton.gameObject.SetActive(isPlanningStep);
        if (closeButton != null) closeButton.gameObject.SetActive(!isPlanningStep);
    }

    // На экране планирования клик мимо окна не закрывает его, чтобы игрок случайно не проскочил шаг цепочки.
    private void BackgroundButton_Clicked()
    {
        if (_isPlanningStep) return;
        Hide();
    }

    private void Open()
    {
        // Паттерн Smart UI: сначала аниматор
        var animator = GetComponent<UIWindowAnimator>();
        if (animator != null) animator.Open();
        else gameObject.SetActive(true);
        
        MainUIManager.Instance?.PushPause();
        
        // Автоматически ищем попап, если забыли привязать в инспекторе
        if (configPopup == null) 
            configPopup = FindFirstObjectByType<ActionConfigPopupUI>(FindObjectsInactive.Include);
            
        RebuildTable();
    }

    public void Hide()
    {
        var animator = GetComponent<UIWindowAnimator>();
        if (animator != null)
        {
            animator.Close();
        }
        else
        {
            gameObject.SetActive(false);
            MainUIManager.Instance?.PopPause();
        }
        
        // Применяем новое расписание сразу после закрытия панели
        HiringManager.Instance?.CheckAllStaffShiftsImmediately();

        OnClosed();
    }

    private void OnClosed() => Closed?.Invoke();

    // Публичный метод для принудительного обновления
    public void RefreshTable()
    {
        if (gameObject.activeInHierarchy)
        {
            RebuildTable();
        }
    }

    // Метод, который будет вызываться при клике на имя сотрудника
    private void OnStaffNameClicked(StaffController staff)
    {
        // На экране планирования тактику не открываем: попап закрывает расписание и тем самым продвинул бы цепочку конца дня.
        if (_isPlanningStep) return;

        if (configPopup != null)
        {
            configPopup.OpenForStaff(staff); // Открываем настройки
            Hide(); // Закрываем расписание, чтобы не мешало
        }
        else
        {
            Debug.LogError("[StaffSchedulePanelUI] ActionConfigPopupUI не найден!");
        }
    }

    public void RebuildTable()
    {
        ClearContainer(periodHeaderContainer);
        ClearContainer(staffNamesContainer);
        ClearContainer(rowsContainer);

        if (HiringManager.Instance == null || TimeManager.Instance == null) return;

        var periods = TimeManager.Instance.mainCalendarDay.periodSettings;

        // 1. Шапка (Периоды)
        foreach (var period in periods)
        {
            GameObject headerObj = Instantiate(periodTitlePrefab, periodHeaderContainer);
            var text = headerObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (text != null) text.text = period.PeriodType.ToString();
        }

        // 2. Списки (Имена и Строки)
        foreach (var staff in HiringManager.Instance.AllStaff)
        {
            if (staff == null) continue;

            // А. Имя
            GameObject nameObj = Instantiate(staffNamePrefab, staffNamesContainer);
            StaffNameLabelUI nameUI = nameObj.GetComponent<StaffNameLabelUI>(); 
            if (nameUI != null)
            {
                // Передаем staff, базу цветов и НАШ МЕТОД ОБРАБОТКИ КЛИКА
                nameUI.Setup(staff, roleColorDb, OnStaffNameClicked);
            }

            // Б. Строка
            GameObject rowObj = Instantiate(scheduleRowPrefab, rowsContainer);
            StaffScheduleRowUI rowUI = rowObj.GetComponent<StaffScheduleRowUI>(); 
            if (rowUI != null)
            {
                rowUI.Setup(staff, periods, IsEditable);
            }
        }
    }

    private void ClearContainer(Transform container)
    {
        foreach (Transform child in container) Destroy(child.gameObject);
    }
}
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Managers;
using Data.Calendar;

public class StaffSchedulePanelUI : MonoBehaviour
{
    [Header("Управление окном")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backgroundButton;

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

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (backgroundButton != null) backgroundButton.onClick.AddListener(Hide);
    }

    private void OnEnable()
    {
        MainUIManager.Instance?.PushPause();
        // Автоматически ищем попап, если забыли привязать в инспекторе
        if (configPopup == null) 
            configPopup = FindFirstObjectByType<ActionConfigPopupUI>(FindObjectsInactive.Include);
            
        RebuildTable();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        MainUIManager.Instance?.PopPause();
    }

    // Метод, который будет вызываться при клике на имя сотрудника
    private void OnStaffNameClicked(StaffController staff)
    {
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
                rowUI.Setup(staff, periods);
            }
        }
    }

    private void ClearContainer(Transform container)
    {
        foreach (Transform child in container) Destroy(child.gameObject);
    }
}
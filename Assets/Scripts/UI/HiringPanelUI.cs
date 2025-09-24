using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Этот скрипт висит на главной панели "Отдел Кадров", где отображается список нанятых сотрудников.
public class HiringPanelUI : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Префаб карточки для отображения одного сотрудника")]
    public GameObject teamMemberCardPrefab;
    [Tooltip("Контейнер, куда будут добавляться карточки")]
    public Transform teamListContent;
    [Tooltip("Префаб 'папки', который будет отображаться в конце списка")]
    public GameObject folderBottomPrefab;
    
    // Перечисление для хранения текущего режима сортировки
    private enum SortMode { ByName, ByRole, ByRank }
    private SortMode currentSortMode = SortMode.ByName;
    // Хранит направление сортировки: true = по возрастанию, false = по убыванию
    private bool isSortAscending = true; 
    
    private List<TeamMemberCardUI> activeCards = new List<TeamMemberCardUI>();

    // Вызывается каждый раз, когда панель становится активной
    void OnEnable()
    {
        // Сбрасываем сортировку по умолчанию при каждом открытии
        currentSortMode = SortMode.ByName;
        isSortAscending = true;
        RefreshTeamList();
    }
    
    /// <summary>
    /// Публичный метод для показа панели.
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        // RefreshTeamList() будет вызван автоматически через OnEnable()
    }

    /// <summary>
    /// Публичный метод для скрытия панели.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Этот метод будет вызываться кнопками сортировки.
    /// 0 = по имени, 1 = по роли, 2 = по рангу.
    /// </summary>
    public void OnSortButtonClicked(int mode)
    {
        SortMode newMode = (SortMode)mode;

        if (newMode == currentSortMode)
        {
            // Если мы кликнули на ту же кнопку, меняем направление
            isSortAscending = !isSortAscending;
        }
        else
        {
            // Если мы выбрали новую колонку, сбрасываем на сортировку по возрастанию
            currentSortMode = newMode;
            isSortAscending = true;
        }

        // Перерисовываем список с новыми параметрами
        RefreshTeamList();
    }

    /// <summary>
    /// Полностью перестраивает и перерисовывает список сотрудников.
    /// </summary>
    public void RefreshTeamList()
    {
        // Шаг 1: Очищаем старые карточки
        foreach (Transform child in teamListContent)
        {
            Destroy(child.gameObject);
        }
        activeCards.Clear();

        if (HiringManager.Instance == null) return;
        
        // Шаг 2: Получаем актуальный список сотрудников
        var allStaff = HiringManager.Instance.AllStaff;

        // Шаг 3: Сортируем список в соответствии с выбранным режимом
        switch (currentSortMode)
        {
            case SortMode.ByName:
                allStaff = isSortAscending 
                    ? allStaff.OrderBy(staff => staff.characterName).ToList() 
                    : allStaff.OrderByDescending(staff => staff.characterName).ToList();
                break;
            case SortMode.ByRole:
                allStaff = isSortAscending 
                    ? allStaff.OrderBy(staff => staff.currentRole.ToString()).ToList() 
                    : allStaff.OrderByDescending(staff => staff.currentRole.ToString()).ToList();
                break;
            case SortMode.ByRank:
                allStaff = isSortAscending 
                    ? allStaff.OrderBy(staff => staff.rank).ToList() 
                    : allStaff.OrderByDescending(staff => staff.rank).ToList();
                break;
        }

        // Шаг 4: Создаем новые карточки для отсортированного списка
        foreach (var staffMember in allStaff)
        {
            if (staffMember == null) continue;
            GameObject cardGO = Instantiate(teamMemberCardPrefab, teamListContent);
            TeamMemberCardUI cardUI = cardGO.GetComponent<TeamMemberCardUI>();
            if (cardUI != null)
            {
                cardUI.Setup(staffMember);
                activeCards.Add(cardUI);
            }
        }
        
        // Шаг 5: Добавляем "папку-замыкающий" в конец
        if (folderBottomPrefab != null && allStaff.Any())
        {
            Instantiate(folderBottomPrefab, teamListContent);
        }
    }
    
    // Обновляет данные на уже существующих карточках в реальном времени
    void Update()
    {
        if (gameObject.activeInHierarchy)
        {
            foreach (var card in activeCards)
            {
                if (card != null)
                {
                    card.UpdateCard();
                }
            }
        }
    }
}
// Файл: Assets/Scripts/UI/ResumePin.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Collections.Generic;
using Utilities; // <--- Убедитесь, что эта строка есть

public class ResumePin : MonoBehaviour
{
    [Header("UI Компоненты 'Листка'")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI roleText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject uniqueSkillIcon;
    [SerializeField] private Button viewButton;

    // Ссылка на базу данных цветов (назначается при Setup)
    private RoleColorDatabase roleColorDb;

    private Candidate candidate;
    private HiringSystemUI panelController;

    // Старый словарь цветов удален

    /// <summary>
    /// Настраивает "листок" данными кандидата и ссылками.
    /// </summary>
    /// <param name="cand">Данные кандидата.</param>
    /// <param name="controller">Ссылка на HiringSystemUI.</param>
    /// <param name="colorDb">Ссылка на базу данных цветов.</param>
    public void Setup(Candidate cand, HiringSystemUI controller, RoleColorDatabase colorDb) // Добавлен colorDb
    {
        this.candidate = cand;
        this.panelController = controller;
        this.roleColorDb = colorDb; // Сохраняем ссылку на базу данных

        // Проверяем кандидата
        if (candidate == null) {
            Debug.LogError("Попытка настроить ResumePin с null Candidate!", gameObject);
            // Можно скрыть объект или показать текст ошибки
            if(nameText != null) nameText.text = "Ошибка";
            if(roleText != null) roleText.text = "";
            gameObject.SetActive(false); // Скрываем некорректный пин
            return;
        }

        // Устанавливаем имя и роль
        if (nameText != null) nameText.text = candidate.Name ?? UI.GameText.Get("hiring.unnamed");
        if (roleText != null)
        {
            var label = roleText.GetComponent<UI.LocalizedText>() ?? roleText.gameObject.AddComponent<UI.LocalizedText>();
            label.SetReference(UI.GameText.Table, UI.StaffRoleNames.GetKey(candidate.Role));
        }

        // Получаем и применяем цвет
        Color bgColor = Color.white;
        if (roleColorDb != null)
        {
            bgColor = roleColorDb.GetColorForRole(candidate.Role, Color.white);
        }
        
        // Если цвет белый (дефолт), пробуем получить цвет по роли напрямую
        if (bgColor == Color.white)
        {
            bgColor = GetDefaultRoleColor(candidate.Role);
        }
        
        // Применяем цвет к основному фону, если он назначен
        if (backgroundImage != null)
        {
            backgroundImage.color = bgColor;
        }

        // Настраиваем иконку уникального навыка
        if (uniqueSkillIcon != null)
        {
            // Проверяем наличие не-null действий в пуле
            uniqueSkillIcon.SetActive(candidate.UniqueActionsPool != null && candidate.UniqueActionsPool.Any(a => a != null));
        }

        // Настраиваем кнопку просмотра
        if (viewButton != null)
        {
            viewButton.onClick.RemoveAllListeners();
            viewButton.onClick.AddListener(OnView);

            // Если backgroundImage не был назначен отдельно (например, фон = сама кнопка),
            // пытаемся взять Image с кнопки и применить цвет
            if (backgroundImage == null)
            {
                Image buttonImage = viewButton.GetComponent<Image>();
                if (buttonImage != null)
                {
                    backgroundImage = buttonImage; // Запоминаем ссылку для будущих вызовов
                    backgroundImage.color = bgColor; // Применяем цвет
                } else {
                     Debug.LogWarning("Не удалось найти Image на кнопке ResumePin для установки цвета фона.", gameObject);
                }
            }
        } else {
             Debug.LogError("Кнопка 'viewButton' не назначена в ResumePin!", gameObject);
        }
    } // Конец Setup

    /// <summary>
    /// Вызывается при нажатии на "листок". Открывает детальный вид.
    /// </summary>
    private void OnView()
    {
        // Проверяем наличие контроллера и кандидата перед открытием
        if (panelController != null && candidate != null)
        {
            panelController.ShowDetailedView(this.candidate, this.gameObject);
        } else {
             Debug.LogError("Невозможно открыть детальный вид: panelController или candidate == null в ResumePin!", gameObject);
        }
    }

    /// <summary>
    /// Вспомогательный метод для получения русского названия роли.
    /// </summary>
    private Color GetDefaultRoleColor(StaffController.Role role)
    {
        switch (role)
        {
            case StaffController.Role.Intern: return new Color(0.6f, 0.8f, 0.4f); // Светло-зелёный
            case StaffController.Role.Clerk: return new Color(0.4f, 0.6f, 0.8f); // Голубой
            case StaffController.Role.Registrar: return new Color(0.7f, 0.5f, 0.8f); // Фиолетовый
            case StaffController.Role.Cashier: return new Color(0.9f, 0.7f, 0.2f); // Золотой
            case StaffController.Role.Archivist: return new Color(0.6f, 0.5f, 0.4f); // Коричневый
            case StaffController.Role.Guard: return new Color(0.3f, 0.3f, 0.5f); // Тёмно-синий
            case StaffController.Role.Janitor: return new Color(0.5f, 0.5f, 0.5f); // Серый
            case StaffController.Role.OfficeManager: return new Color(0.9f, 0.4f, 0.4f); // Красный
            case StaffController.Role.Accountant: return new Color(0.78f, 0.72f, 0.25f); // Оливковый (как у Кассира, чуть зеленее)
            case StaffController.Role.ServiceWorker: return new Color(0.8f, 0.5f, 0.3f); // Оранжевый
            default: return Color.white;
        }
    }
}

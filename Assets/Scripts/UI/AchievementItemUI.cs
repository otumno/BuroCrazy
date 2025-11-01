// Файл: Assets/Scripts/UI/AchievementItemUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class AchievementItemUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private CanvasGroup canvasGroup; // Для затемнения

    private AchievementData data;
    private AchievementListUI listController;

    /// <summary>
    /// Настраивает ячейку
    /// </summary>
    public void Setup(AchievementData achievementData, AchievementListUI controller, bool isUnlocked)
    {
        this.data = achievementData;
        this.listController = controller;

        // "Открыт доступ к архивной записи "Название""
        nameText.text = data.displayName;
        descriptionText.text = data.description;
        
        Button thisButton = GetComponent<Button>();

        if (isUnlocked)
        {
            icon.sprite = data.iconUnlocked; // Цветная иконка [cite: 1409]
            canvasGroup.alpha = 1f; // Полная яркость
            thisButton.interactable = true; // Можно нажать
            thisButton.onClick.AddListener(OnItemClick);
        }
        else
        {
            icon.sprite = data.iconLocked; // Ч/Б иконка [cite: 1409]
            canvasGroup.alpha = 0.5f; // Полупрозрачный
            thisButton.interactable = false; // Нельзя нажать
            
            // Если ачивка секретная, скрываем всю инфу
            if (data.isSecret)
            {
                nameText.text = "???";
                descriptionText.text = "Это достижение пока скрыто.";
            }
        }
    }

    /// <summary>
    /// Вызывается при клике на разблокированную ачивку
    /// </summary>
    private void OnItemClick()
    {
        // Сообщаем главному контроллеру списка, что нас нажали
        listController.OnAchievementClicked(data);
    }
}
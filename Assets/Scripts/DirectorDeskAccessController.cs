// Файл: DirectorDeskAccessController.cs
using UnityEngine;
using UnityEngine.UI;

public class DirectorDeskAccessController : MonoBehaviour
{
    [Header("Кнопки для блокировки")]
    [Tooltip("Кнопка-стопка 'Документы на рассмотрении'")]
    public Button documentsPendingButton; 
    
    [Tooltip("Кнопка 'Кабинет/Пауза' из InGameUI")]
    public Button cabinetPauseButton;

    [Header("Подсказка")]
    [Tooltip("Иконка, которая появляется, когда кнопки заблокированы")]
    public GameObject tooltipIcon;

    void Update()
    {
        // Проверяем, существует ли аватар директора и находится ли он на рабочем месте
        bool isDirectorAtDesk = DirectorAvatarController.Instance != null && DirectorAvatarController.Instance.IsAtDesk;

        // Включаем или выключаем интерактивность кнопок
        if (documentsPendingButton != null)
        {
            documentsPendingButton.interactable = isDirectorAtDesk;
        }
        if (cabinetPauseButton != null)
        {
            cabinetPauseButton.interactable = isDirectorAtDesk;
        }

        // Показываем или прячем иконку-подсказку
        if (tooltipIcon != null)
        {
            // Иконка видна, когда директор НЕ на рабочем месте
            tooltipIcon.SetActive(!isDirectorAtDesk);
        }
    }
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class OrderSelectionUI : MonoBehaviour
{
    [SerializeField] private List<OrderCardUI> orderCards; // Ссылки на 3 карточки приказа
    [SerializeField] private CanvasGroup canvasGroup;
    
    private List<DirectorOrder> currentOfferedOrders;

    // Этот метод будет вызываться из MainUIManager, чтобы показать панель
    public void Setup()
    {
        // Просим у DirectorManager три случайных приказа на выбор
        currentOfferedOrders = DirectorManager.Instance.GetRandomOrders(3);

        // Настраиваем каждую карточку
        for (int i = 0; i < orderCards.Count; i++)
        {
            if (i < currentOfferedOrders.Count)
            {
                // "Знакомим" карточку с ее приказом и даем ей номер
                orderCards[i].Setup(currentOfferedOrders[i], i);
                // Подписываемся на событие клика
                orderCards[i].OnOrderSelected += HandleOrderSelection; 
                orderCards[i].gameObject.SetActive(true);
            }
            else
            {
                orderCards[i].gameObject.SetActive(false);
            }
        }
    }

    // Этот метод сработает, когда мы нажмем на любую из карточек
    private void HandleOrderSelection(int selectedIndex)
{
    // 1. Сообщаем DirectorManager, какой приказ мы выбрали
    DirectorManager.Instance.SelectOrder(currentOfferedOrders[selectedIndex]);
    
    // 2. Отписываемся от событий, чтобы избежать багов
    foreach (var card in orderCards)
    {
        card.OnOrderSelected -= HandleOrderSelection;
    }

    // 3. Находим стол директора и делаем его интерактивным
    var startOfDayPanel = FindFirstObjectByType<StartOfDayPanel>();
    if (startOfDayPanel != null)
    {
        startOfDayPanel.UpdatePanelInfo();
        var cg = startOfDayPanel.GetComponent<CanvasGroup>();
        cg.interactable = true;
        cg.blocksRaycasts = true;
    }

    // 4. Просто выключаем себя, чтобы показать слой ниже.
    gameObject.SetActive(false);
}
    
    // Корутина для плавного появления/исчезновения
    public IEnumerator Fade(bool fadeIn)
{
    float targetAlpha = fadeIn ? 1f : 0f;
    if(fadeIn)
    {
        gameObject.SetActive(true);
    }

    // Делаем панель интерактивной СРАЗУ, как только она начинает появляться
    canvasGroup.interactable = fadeIn;
    canvasGroup.blocksRaycasts = fadeIn;

    float elapsedTime = 0f;
    float duration = 0.3f;
    float startAlpha = canvasGroup.alpha;
    while (elapsedTime < duration)
    {
        canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration);
        elapsedTime += Time.unscaledDeltaTime;
        yield return null;
    }
    canvasGroup.alpha = targetAlpha;

    if(!fadeIn)
    {
        gameObject.SetActive(false);
    }
}
}
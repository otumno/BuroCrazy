using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Managers;

[RequireComponent(typeof(CanvasGroup))]
public class OrderSelectionUI : MonoBehaviour
{
    [Header("UI Компоненты")]
    [SerializeField] private List<OrderCardUI> orderCards;
    private CanvasGroup canvasGroup;

    private void Awake() { canvasGroup = GetComponent<CanvasGroup>(); }

    public void Setup()
    {
        if (OrderManager.Instance == null)
            return;
        
        List<DirectorOrder> availableOrders = OrderManager.Instance.GetAvailableOrdersForDay();
        
        for (int i = 0; i < orderCards.Count; i++)
        {
            if (i < availableOrders.Count)
            {
                orderCards[i].Setup(availableOrders[i], OnOrderSelected);
                orderCards[i].gameObject.SetActive(true);
            }
            else
            {
                orderCards[i].gameObject.SetActive(false);
            }
        }
    }

    private void OnOrderSelected(DirectorOrder selectedOrder)
    {
        // ИСПРАВЛЕНИЕ: Используем OrderManager
        if (OrderManager.Instance != null)
        {
            OrderManager.Instance.SelectOrder(selectedOrder);
        }
        StartCoroutine(UpdateAndFadeOut());
    }

    private IEnumerator UpdateAndFadeOut()
    {
        yield return StartCoroutine(Fade(false));
        yield return new WaitForEndOfFrame();

        if (StartOfDayPanel.Instance != null)
        {
            StartOfDayPanel.Instance.UpdatePanelInfo();
        }
    }

    public IEnumerator Fade(bool fadeIn)
    {
        float targetAlpha = fadeIn ? 1f : 0f;
        float startAlpha = canvasGroup.alpha;
        float fadeDuration = 0.5f;
        canvasGroup.interactable = fadeIn;
        canvasGroup.blocksRaycasts = fadeIn; // лучше всегда блочить рэйкасты, пока идёт анимация, иначе игрок сквозь экран прокликает
        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = targetAlpha;
    }
}
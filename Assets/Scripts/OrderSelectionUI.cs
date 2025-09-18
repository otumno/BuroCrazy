using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(CanvasGroup))]
public class OrderSelectionUI : MonoBehaviour
{
    [Header("UI Компоненты")]
    [SerializeField] private List<OrderCardUI> orderCards;
    private CanvasGroup canvasGroup;

    private void Awake() { canvasGroup = GetComponent<CanvasGroup>(); }

    public void Setup()
    {
        if (DirectorManager.Instance == null) return;
        List<DirectorOrder> availableOrders = DirectorManager.Instance.GetAvailableOrdersForDay();
        for (int i = 0; i < orderCards.Count; i++)
        {
            if (i < availableOrders.Count)
            {
                orderCards[i].Setup(availableOrders[i], this);
                orderCards[i].gameObject.SetActive(true);
            }
            else
            {
                orderCards[i].gameObject.SetActive(false);
            }
        }
    }

    public void OnOrderSelected(DirectorOrder selectedOrder)
    {
        if (DirectorManager.Instance != null)
        {
            DirectorManager.Instance.SelectOrder(selectedOrder);
        }
        StartCoroutine(UpdateAndFadeOut());
    }

    // <<< НОВАЯ ОБЪЕДИНЕННАЯ КОРУТИНА >>>
    private IEnumerator UpdateAndFadeOut()
    {
        // Сначала плавно прячем эту панель
        yield return StartCoroutine(Fade(false));

        // Затем ждем конца кадра, чтобы все изменения применились
        yield return new WaitForEndOfFrame();

        // И только теперь надежно обновляем панель стола
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
        canvasGroup.blocksRaycasts = fadeIn;
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
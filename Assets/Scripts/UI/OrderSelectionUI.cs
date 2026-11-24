using System;
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
    private bool _selectionInProgress;
    private bool _forNextDay;
    private bool _hasChoices;

    /// <summary>
    /// Игрок выбрал приказ. Сам выбор закрывает экран.
    /// </summary>
    public event Action OrderSelected;

    private void Awake() { canvasGroup = GetComponent<CanvasGroup>(); }

    /// <summary>
    /// Показывает выбор приказа с плавным появлением (цепочка конца дня).
    /// </summary>
    public void Show()
    {
        ShowInternal(false);
    }

    public void ShowForNextDay() => ShowInternal(true);

    private void ShowInternal(bool forNextDay)
    {
        _forNextDay = forNextDay;
        gameObject.SetActive(true);
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        Setup();
        if (!_hasChoices) return;
        StopAllCoroutines();
        StartCoroutine(Fade(true));
    }

    public void Setup()
    {
        _selectionInProgress = false;
        _hasChoices = false;
        StopAllCoroutines();
        if (OrderManager.Instance == null)
        {
            Debug.LogError("[OrderSelectionUI] OrderManager отсутствует; этап выбора пропущен.");
            gameObject.SetActive(false);
            OnOrderSelected();
            return;
        }
        
        List<DirectorOrder> availableOrders = OrderManager.Instance.GetAvailableOrdersForDay();
        _hasChoices = availableOrders.Count > 0 && orderCards != null && orderCards.Exists(c => c != null);
        if (!_hasChoices)
        {
            // Exhausting one-time choices must not trap the night sequence on an empty screen.
            Debug.LogWarning("[OrderSelectionUI] Доступных приказов нет; этап выбора пропущен.");
            gameObject.SetActive(false);
            OnOrderSelected();
            return;
        }
        
        int orderIndex = 0;
        for (int i = 0; i < orderCards.Count; i++)
        {
            if (orderCards[i] == null) continue;
            if (orderIndex < availableOrders.Count)
            {
                orderCards[i].Setup(availableOrders[orderIndex++], OrderCard_Clicked);
                orderCards[i].gameObject.SetActive(true);
            }
            else
            {
                orderCards[i].gameObject.SetActive(false);
            }
        }
    }

    private void OrderCard_Clicked(DirectorOrder selectedOrder)
    {
        if (_selectionInProgress || selectedOrder == null) return;
        _selectionInProgress = true;
        // Иначе незавершённое появление окна продолжает повышать alpha во время закрытия.
        StopAllCoroutines();
        // ИСПРАВЛЕНИЕ: Используем OrderManager
        if (OrderManager.Instance != null)
        {
            if (_forNextDay) OrderManager.Instance.SelectOrderForNextDay(selectedOrder);
            else OrderManager.Instance.SelectOrder(selectedOrder);
        }
        StartCoroutine(UpdateAndFadeOut());
    }

    private void OnOrderSelected() => OrderSelected?.Invoke();

    private IEnumerator UpdateAndFadeOut()
    {
        yield return StartCoroutine(Fade(false));
        yield return new WaitForEndOfFrame();

        if (StartOfDayPanel.Instance != null)
        {
            StartOfDayPanel.Instance.UpdatePanelInfo();
        }
        // День меняем только после закрытия окна, чтобы не оставить его поверх стола/катсцены.
        OnOrderSelected();
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

// Файл: OrderSelectionUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class OrderSelectionUI : MonoBehaviour
{
    [Header("UI Components")]
    public List<Button> orderButtons;
    public List<TextMeshProUGUI> titleTexts;
    public List<TextMeshProUGUI> descriptionTexts;
    public List<Image> iconImages;

    [Tooltip("Перетащите сюда компонент Canvas Group с этой панели")]
    public CanvasGroup canvasGroup;

    [Header("Целевые точки для анимации")]
    public List<RectTransform> buttonTargetPositions;
    
    [Header("Manager References")]
    public MenuManager menuManager;
    public StartOfDayPanel startOfDayPanel;
    
    [Header("Настройки анимации")]
    public float buttonFallDuration = 0.4f;
    public AudioClip buttonLandSound;
    [Header("Случайная задержка между кнопками")]
    public float minDelayBetweenButtons = 0.1f;
    public float maxDelayBetweenButtons = 0.25f;

    private List<DirectorOrder> currentOfferedOrders;
    private bool isSetupRunning = false;

    void OnEnable()
    {
        if (!isSetupRunning)
        {
            StartCoroutine(SetupRoutine());
        }
    }

    void OnDisable()
    {
        isSetupRunning = false;
    }

    private IEnumerator SetupRoutine()
    {
        isSetupRunning = true;
        
        // --- ИЗМЕНЕНИЕ №1: СРАЗУ ВКЛЮЧАЕМ ИНТЕРАКТИВНОСТЬ ---
        if (canvasGroup != null) 
        {
            canvasGroup.interactable = true;
        }
        
        foreach (var button in orderButtons)
        {
            if(button != null) button.gameObject.SetActive(true);
        }

        yield return new WaitForEndOfFrame();
        
        SetupAndAnimate();
    }

    public void SetupAndAnimate()
    {
        if (DirectorManager.Instance == null) { isSetupRunning = false; return; }
        currentOfferedOrders = DirectorManager.Instance.offeredOrders;
        if (currentOfferedOrders == null) { isSetupRunning = false; return; }
        
        for (int i = 0; i < orderButtons.Count; i++)
        {
            if (i < currentOfferedOrders.Count)
            {
                orderButtons[i].gameObject.SetActive(true);
                DirectorOrder order = currentOfferedOrders[i];
                titleTexts[i].text = order.orderName;
                descriptionTexts[i].text = order.orderDescription;
                iconImages[i].sprite = order.icon;

                orderButtons[i].onClick.RemoveAllListeners();
                int orderIndex = i;
                orderButtons[i].onClick.AddListener(() => OnOrderSelected(orderIndex));
            }
            else
            {
                orderButtons[i].gameObject.SetActive(false);
            }
        }
        
        StartCoroutine(AnimateButtonsIn());
    }

    private void OnOrderSelected(int index)
    {
        if (index < 0 || currentOfferedOrders == null || index >= currentOfferedOrders.Count) return;
        DirectorOrder selectedOrder = currentOfferedOrders[index];
        DirectorManager.Instance.SelectOrder(selectedOrder);
        if (startOfDayPanel != null) { startOfDayPanel.UpdatePanelInfo(); }
        gameObject.SetActive(false);
    }
    
    private IEnumerator AnimateButtonsIn()
    {
        for (int i = 0; i < orderButtons.Count; i++)
        {
            if (i < currentOfferedOrders.Count && orderButtons[i] != null)
            {
                if (i >= buttonTargetPositions.Count || buttonTargetPositions[i] == null)
                {
                    Debug.LogError($"Целевая точка для кнопки {i} не настроена!");
                    continue;
                }
                
                RectTransform buttonRect = orderButtons[i].GetComponent<RectTransform>();
                Vector3 endPosition = buttonTargetPositions[i].anchoredPosition;
                
                buttonRect.anchoredPosition = new Vector2(endPosition.x, endPosition.y + 700f);

                yield return StartCoroutine(MoveButton(buttonRect, endPosition));
                
                float delay = Random.Range(minDelayBetweenButtons, maxDelayBetweenButtons);
                yield return new WaitForSecondsRealtime(delay);
            }
        }
        
        // --- ИЗМЕНЕНИЕ №2: Этот блок больше не нужен, так как мы включили интерактивность в самом начале ---
        /*
        if (canvasGroup != null)
        {
            canvasGroup.interactable = true;
        }
        */
        
        isSetupRunning = false;
    }
    
    private IEnumerator MoveButton(RectTransform buttonTransform, Vector3 endPosition)
    {
        if (buttonLandSound != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(buttonLandSound, Camera.main.transform.position, 0.7f);
        }
        Vector3 startPosition = buttonTransform.anchoredPosition;
        float elapsedTime = 0f;
        while (elapsedTime < buttonFallDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float progress = elapsedTime / buttonFallDuration;
            float easedProgress = 1 - Mathf.Pow(1 - progress, 3);
            buttonTransform.anchoredPosition = Vector3.LerpUnclamped(startPosition, endPosition, easedProgress);
            yield return null;
        }
        buttonTransform.anchoredPosition = endPosition;
    }
}
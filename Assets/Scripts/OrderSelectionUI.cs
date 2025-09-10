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
    public CanvasGroup canvasGroup;
    
    [Header("Целевые точки для анимации")]
    public List<RectTransform> buttonTargetPositions;
    
    [Header("Manager References")]
    public MenuManager menuManager;
    public StartOfDayPanel startOfDayPanel;
    
    [Header("Настройки анимации")]
    public float buttonFallDuration = 0.4f;
    public AudioClip buttonLandSound;
    public float minDelayBetweenButtons = 0.1f;
    public float maxDelayBetweenButtons = 0.25f;

    private List<DirectorOrder> currentOfferedOrders;
    private bool isSetupRunning = false;

    void Start()
    {
        if (menuManager == null) menuManager = MenuManager.Instance;
        if (startOfDayPanel == null) startOfDayPanel = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include);
    }

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
        if (canvasGroup != null) 
        {
            canvasGroup.interactable = false;
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
        
        // --- ГЛАВНОЕ ИЗМЕНЕНИЕ ---
        // Вместо запуска игры, мы просто прячем эту панель
        gameObject.SetActive(false);
        
        // И просим стол директора обновить информацию (показать выбранный приказ)
        if (startOfDayPanel != null)
        {
            startOfDayPanel.UpdatePanelInfo();
        }
    }
    
    private IEnumerator AnimateButtonsIn()
    {
        for (int i = 0; i < orderButtons.Count; i++)
        {
            if (i < currentOfferedOrders.Count && orderButtons[i] != null)
            {
                if (i >= buttonTargetPositions.Count || buttonTargetPositions[i] == null)
                {
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
        
        if (canvasGroup != null)
        {
            canvasGroup.interactable = true;
        }

        isSetupRunning = false;
    }
    
    private IEnumerator MoveButton(RectTransform buttonTransform, Vector3 endPosition)
    {
        if (buttonLandSound != null && menuManager != null && menuManager.uiAudioSource != null)
        {
            menuManager.uiAudioSource.PlayOneShot(buttonLandSound);
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
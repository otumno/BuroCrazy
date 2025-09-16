// Файл: OrderSelectionUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(CanvasGroup))]
public class OrderSelectionUI : MonoBehaviour
{
    [Header("UI Components")]
    public List<Button> orderButtons;
    public List<TextMeshProUGUI> titleTexts;
    public List<TextMeshProUGUI> descriptionTexts;
    public List<Image> iconImages;
    
    [Header("Настройки анимации и звуков")]
    public AudioClip buttonLandSound;
    public AudioClip orderSelectedSound;

    private CanvasGroup canvasGroup;
    private List<DirectorOrder> currentOfferedOrders;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    // Этот метод вызывается из GameSceneUIManager для настройки панели
    public void Setup()
    {
        if (DirectorManager.Instance == null) return;
        
        currentOfferedOrders = DirectorManager.Instance.offeredOrders;
        if (currentOfferedOrders == null) return;
        
        for (int i = 0; i < orderButtons.Count; i++)
        {
            if (i < currentOfferedOrders.Count && currentOfferedOrders[i] != null)
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
    }

    private void OnOrderSelected(int index)
    {
        if (index < 0 || currentOfferedOrders == null || index >= currentOfferedOrders.Count) return;
        
        if (MainUIManager.Instance?.uiAudioSource != null && orderSelectedSound != null)
        {
            MainUIManager.Instance.uiAudioSource.PlayOneShot(orderSelectedSound);
        }
        
        DirectorOrder selectedOrder = currentOfferedOrders[index];
        DirectorManager.Instance.SelectOrder(selectedOrder);
        
        // Прячем себя и показываем главный стол директора
        StartCoroutine(Fade(false));
        if (StartOfDayPanel.Instance != null)
        {
            StartOfDayPanel.Instance.ShowPanel();
        }
    }

    // Корутина для плавного появления/исчезновения
    public IEnumerator Fade(bool fadeIn)
    {
        float fadeTime = 0.3f;
        float startAlpha = fadeIn ? 0f : 1f;
        float endAlpha = fadeIn ? 1f : 0f;
        
        canvasGroup.blocksRaycasts = true;
        
        float timer = 0f;
        while (timer < fadeTime)
        {
            // Используем unscaledDeltaTime, так как игра может быть на паузе
            timer += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, timer / fadeTime);
            yield return null;
        }
        
        canvasGroup.alpha = endAlpha;
        canvasGroup.interactable = fadeIn;
        canvasGroup.blocksRaycasts = fadeIn;
    }
}
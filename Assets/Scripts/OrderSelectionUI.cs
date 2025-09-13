// Файл: OrderSelectionUI.cs - Упрощенная версия
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
	
	[Header("Звуки")]
	public AudioClip orderSelectedSound;

    private CanvasGroup canvasGroup;
    private List<DirectorOrder> currentOfferedOrders;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }
    
    public void Setup()
    {
        if (DirectorManager.Instance == null) return;
        currentOfferedOrders = DirectorManager.Instance.offeredOrders;
        if (currentOfferedOrders == null) return;
        
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
    }

    private void OnOrderSelected(int index)
    {
        if (index < 0 || currentOfferedOrders == null || index >= currentOfferedOrders.Count) return;
        
		    if (MenuManager.Instance?.uiAudioSource != null && orderSelectedSound != null)
    {
        MenuManager.Instance.uiAudioSource.PlayOneShot(orderSelectedSound);
    }
		
        DirectorOrder selectedOrder = currentOfferedOrders[index];
        DirectorManager.Instance.SelectOrder(selectedOrder);
        
        // Просто выключаем себя и показываем главный стол
        StartCoroutine(Fade(false));
        StartOfDayPanel.Instance.ShowPanel();
    }

    public IEnumerator Fade(bool fadeIn)
    {
        float fadeTime = 0.3f; // Можно вынести в настройки
        float startAlpha = fadeIn ? 0f : 1f;
        float endAlpha = fadeIn ? 1f : 0f;
        
        canvasGroup.blocksRaycasts = true;
        
        float timer = 0f;
        while (timer < fadeTime)
        {
            timer += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, timer / fadeTime);
            yield return null;
        }
        
        canvasGroup.alpha = endAlpha;
        canvasGroup.interactable = fadeIn;
        canvasGroup.blocksRaycasts = fadeIn;
    }
}
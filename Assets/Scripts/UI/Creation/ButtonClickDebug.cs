using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Data.Creation;

public class ButtonClickDebug : MonoBehaviour, IPointerClickHandler
{
    public string buttonText;
    public BookPageData.BookChoice choiceData;
    public System.Action<BookPageData.BookChoice> onClicked;
    
    private Button button;
    
    private void Awake()
    {
        button = GetComponent<Button>();
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[ButtonClickDebug] ====> IPointerClickHandler вызван для: {buttonText}");
        
        if (button != null)
        {
            Debug.Log($"[ButtonClickDebug] Button interactable: {button.interactable}");
        }
        
        if (onClicked != null && choiceData != null)
        {
            onClicked.Invoke(choiceData);
        }
    }
}

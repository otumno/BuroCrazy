using UnityEngine;
using UnityEngine.UI; // <-- ВАЖНО: Добавлена эта строка для работы с ScrollRect
using UnityEngine.EventSystems;
using TMPro;
using System.Collections; // <-- ВАЖНО: Добавлена эта строка для работы с корутинами

public class InfoPopupManager : MonoBehaviour
{
    public static InfoPopupManager Instance { get; private set; }

    [Header("UI Компоненты")]
    public GameObject popupPanel;
    public TextMeshProUGUI infoText;
    [Tooltip("Перетащите сюда компонент Scroll Rect с вашей InfoPopupPanel")]
    public ScrollRect popupScrollRect; // <-- НОВОЕ ПОЛЕ

    private Transform targetToFollow;
    private Camera mainCamera;

    void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); } else { Instance = this; } }
    void Start() { mainCamera = Camera.main; if (popupPanel != null) popupPanel.SetActive(false); }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;

            RaycastHit2D hit = Physics2D.Raycast(mainCamera.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
            if (hit.collider != null && hit.collider.GetComponent<ClickableCharacter>() != null)
            {
                ShowInfoFor(hit.collider.gameObject);
            }
            else
            {
                HideInfo();
            }
        }
        
        if (popupPanel != null && popupPanel.activeSelf && targetToFollow != null)
        {
            popupPanel.transform.position = mainCamera.WorldToScreenPoint(targetToFollow.position + Vector3.up * 1.5f);
        }
    }

    public void ShowInfoFor(GameObject character)
    {
        if (popupPanel == null || infoText == null) return;

        string statusTextResult = "Статус не определен";
        
        CharacterStateLogger logger = character.GetComponent<CharacterStateLogger>();
        if(logger != null && logger.stateHistory.Count > 0)
        {
            string currentStatus = logger.GetCurrentStatus();
            string history = logger.GetFormattedHistory();
            
            statusTextResult = $"<b><color=yellow>{currentStatus}</color></b>";

            if (!string.IsNullOrEmpty(history))
            {
                statusTextResult += $"\n\n<b><color=#cccccc>--- История ---</color></b>\n{history}";
            }
        }
        else 
        {
            ClientStateMachine csm = character.GetComponent<ClientStateMachine>();
            if (csm != null) statusTextResult = csm.GetStatusInfo();

            ClerkController clerk = character.GetComponent<ClerkController>();
            if (clerk != null) statusTextResult = clerk.GetStatusInfo();
            
            GuardMovement guard = character.GetComponent<GuardMovement>();
            if (guard != null) statusTextResult = guard.GetStatusInfo();
        }

        infoText.text = statusTextResult;
        targetToFollow = character.transform;
        popupPanel.SetActive(true);

        // --- НОВАЯ ЛОГИКА: ЗАПУСКАЕМ КОРУТИНУ ДЛЯ ПРОКРУТКИ ВВЕРХ ---
        StartCoroutine(ForceScrollToTop());
    }

    // --- НОВАЯ КОРУТИНА ---
    private IEnumerator ForceScrollToTop()
    {
        // Ждем до конца кадра, чтобы UI успел пересчитать все размеры
        yield return new WaitForEndOfFrame();
        
        if (popupScrollRect != null)
        {
            // Принудительно ставим позицию скролла на самый верх (1 = верх, 0 = низ)
            popupScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    public void HideInfo() 
{ 
    if (popupPanel != null) 
    { 
        popupPanel.SetActive(false); 
        targetToFollow = null; // <-- ДОБАВЬТЕ ЭТУ СТРОКУ
    } 
}
}
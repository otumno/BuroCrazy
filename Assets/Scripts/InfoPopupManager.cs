using UnityEngine;
using UnityEngine.EventSystems; // Необходимо для проверки кликов по UI
using TMPro;

public class InfoPopupManager : MonoBehaviour
{
    public static InfoPopupManager Instance { get; private set; }

    [Header("UI Компоненты")]
    [Tooltip("Панель, которая будет всплывать")]
    public GameObject popupPanel;
    [Tooltip("Текстовое поле для вывода информации")]
    public TextMeshProUGUI infoText;

    private Transform targetToFollow;
    private Camera mainCamera;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        mainCamera = Camera.main;
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }
    }

    void Update()
    {
        // --- НОВАЯ ЛОГИКА ОБРАБОТКИ КЛИКОВ ---
        if (Input.GetMouseButtonDown(0))
        {
            // Проверяем, не кликнули ли мы по UI элементу
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return; // Если да, ничего не делаем
            }

            RaycastHit2D hit = Physics2D.Raycast(mainCamera.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
            if (hit.collider != null && hit.collider.GetComponent<ClickableCharacter>() != null)
            {
                ShowInfoFor(hit.collider.gameObject);
            }
            else
            {
                // Если кликнули мимо персонажа, закрываем окно
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

        string status = "Статус не определен";

        ClientStateMachine csm = character.GetComponent<ClientStateMachine>();
        if (csm != null) status = csm.GetStatusInfo();

        ClerkController clerk = character.GetComponent<ClerkController>();
        if (clerk != null) status = clerk.GetStatusInfo();
        
        GuardMovement guard = character.GetComponent<GuardMovement>();
        if (guard != null) status = guard.GetStatusInfo();

        infoText.text = status;
        targetToFollow = character.transform;
        popupPanel.SetActive(true);
    }

    public void HideInfo()
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
            targetToFollow = null;
        }
    }
}
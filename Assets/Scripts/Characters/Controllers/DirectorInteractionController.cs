using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DirectorInteractionController : MonoBehaviour
{
    [Header("Ссылки на UI")]
    [SerializeField] private Button contextButton;
    [SerializeField] private TextMeshProUGUI contextButtonText;

    private InteractionPoint currentInteractionPoint;
    private DirectorAvatarController directorAvatar;

    void Awake()
    {
        directorAvatar = GetComponent<DirectorAvatarController>();
    }

    void Start()
    {
        if (contextButton != null)
        {
            contextButton.onClick.AddListener(OnContextButtonClicked);
        }
        UpdateContextButton();
    }

    // Убираем Update(), чтобы кнопка не моргала
    // void Update() { ... }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<InteractionPoint>() is InteractionPoint point)
        {
            currentInteractionPoint = point;
            UpdateContextButton();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<InteractionPoint>() is InteractionPoint point && point == currentInteractionPoint)
        {
            currentInteractionPoint = null;
            UpdateContextButton();
        }
    }

    private void UpdateContextButton()
    {
        if (contextButton == null || currentInteractionPoint == null || directorAvatar == null)
        {
            contextButton?.gameObject.SetActive(false);
            return;
        }

        bool shouldBeActive = true;
        string buttonText = "";
        
        // --- НОВАЯ ЛОГИКА: Проверяем, не работает ли Директор уже на этой станции ---
        bool isWorkingHere = directorAvatar.GetCurrentState() == DirectorAvatarController.DirectorState.WorkingAtStation 
                             && directorAvatar.GetWorkstation() == currentInteractionPoint.GetComponentInParent<ServicePoint>();

        switch (currentInteractionPoint.type)
        {
            case InteractionPoint.InteractionType.BarrierControl:
                buttonText = SecurityBarrier.Instance.IsActive() ? "Открыть дверь" : "Закрыть дверь";
                break;
            case InteractionPoint.InteractionType.CollectDocuments:
                buttonText = "Забрать документы";
                shouldBeActive = currentInteractionPoint.associatedStack != null && !currentInteractionPoint.associatedStack.IsEmpty;
                break;
            case InteractionPoint.InteractionType.WorkAtRegistration:
                buttonText = isWorkingHere ? "Закончить работу" : "Работать в регистратуре";
                break;
            case InteractionPoint.InteractionType.WorkAtOfficeDesk:
                buttonText = isWorkingHere ? "Закончить работу" : "Обработать документ";
                break;
            case InteractionPoint.InteractionType.WorkAtCashier:
                buttonText = isWorkingHere ? "Закончить работу" : "Работать в кассе";
                break;
            default:
                shouldBeActive = false;
                break;
        }

        contextButton.gameObject.SetActive(shouldBeActive);
        if (shouldBeActive)
        {
            contextButtonText.text = buttonText;
        }
    }

    private void OnContextButtonClicked()
    {
        if (currentInteractionPoint == null || directorAvatar == null) return;

        ServicePoint workstation = currentInteractionPoint.GetComponentInParent<ServicePoint>();
        bool isWorkingHere = directorAvatar.GetCurrentState() == DirectorAvatarController.DirectorState.WorkingAtStation && directorAvatar.GetWorkstation() == workstation;

        switch (currentInteractionPoint.type)
        {
            case InteractionPoint.InteractionType.BarrierControl:
                if (SecurityBarrier.Instance.IsActive()) SecurityBarrier.Instance.DeactivateBarrier();
                else SecurityBarrier.Instance.ActivateBarrier();
                break;
            case InteractionPoint.InteractionType.CollectDocuments:
                if (currentInteractionPoint.associatedStack != null)
                {
                    directorAvatar.CollectDocuments(currentInteractionPoint.associatedStack);
                }
                break;
            // --- НОВАЯ ЛОГИКА: Включаем или выключаем рабочий режим ---
            case InteractionPoint.InteractionType.WorkAtRegistration:
            case InteractionPoint.InteractionType.WorkAtOfficeDesk:
            case InteractionPoint.InteractionType.WorkAtCashier:
                if (isWorkingHere)
                {
                    directorAvatar.StopManualWork();
                }
                else if (workstation != null)
                {
                    directorAvatar.StartWorkingAt(workstation);
                }
                break;
        }
        
        // Сразу обновляем кнопку после нажатия
        UpdateContextButton();
    }
}
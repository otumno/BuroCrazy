using Gameplay;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Utilities;

public class DirectorInteractionController : MonoBehaviour
{
    [Header("Ссылки на UI")]
    [SerializeField] private Button contextButton;
    [SerializeField] private TextMeshProUGUI contextButtonText;

    private InteractionPoint currentInteractionPoint;
    private DirectorAvatarController directorAvatar;

    private void Awake()
    {
        directorAvatar = GetComponent<DirectorAvatarController>();
    }

    private void Start()
    {
        if (contextButton != null)
        {
            contextButton.onClick.AddListener(OnContextButtonClicked);
        }
        UpdateContextButton();
    }

    // Убираем Update(), чтобы кнопка не моргала
    // private void Update() { ... }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<InteractionPoint>() is not { } point)
            return;

        currentInteractionPoint = point;

        // Во время туториала первого дня директора водит катсцену, поэтому контекстные действия надо игнорировать
        if (FirstDayTutorial.IsActive)
        {
            if (contextButton != null)
                contextButton.gameObject.SetActive(false);
            
            return;
        }

        // --- АВТОЗАПУСК ТУТОРИАЛА ---
        if (point.type == InteractionPoint.InteractionType.TutorialTakeDoc)
        {
            // Скрываем кнопку контекстного действия, чтобы она не мелькала
            if (contextButton != null) contextButton.gameObject.SetActive(false);
                
            // Сразу запускаем катсцену
            if (directorAvatar != null && Managers.TutorialBureaucracyQuest.Instance != null)
            {
                directorAvatar.StartCoroutine(directorAvatar.TutorialAutoTourRoutine(Managers.TutorialBureaucracyQuest.Instance));
            }
            return; // Прерываем метод, чтобы кнопка не обновилась
        }
        // ----------------------------

        UpdateContextButton();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<InteractionPoint>() is not { } point || point != currentInteractionPoint)
            return;
        
        currentInteractionPoint = null;
        UpdateContextButton();
    }

    private void UpdateContextButton()
    {
        if (contextButton == null || currentInteractionPoint == null || directorAvatar == null)
        {
            if (contextButton) contextButton.gameObject.SetActive(false);
            return;
        }

        // Во время туториала первого дня контекстные действия запрещены.
        if (FirstDayTutorial.IsActive)
        {
            contextButton.gameObject.SetActive(false);
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
                buttonText = Managers.GuardManager.Instance.securityBarrier.IsActive() ? "Открыть дверь" : "Закрыть дверь";
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
            case InteractionPoint.InteractionType.TutorialTakeDoc:
                buttonText = "Взять приказ и оформить";
                shouldBeActive = true;
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
        if (FirstDayTutorial.IsActive) return;

        ServicePoint workstation = currentInteractionPoint.GetComponentInParent<ServicePoint>();
        bool isWorkingHere = directorAvatar.GetCurrentState() == DirectorAvatarController.DirectorState.WorkingAtStation && directorAvatar.GetWorkstation() == workstation;

        switch (currentInteractionPoint.type)
        {
            case InteractionPoint.InteractionType.BarrierControl:
                if (Managers.GuardManager.Instance.securityBarrier.IsActive()) Managers.GuardManager.Instance.securityBarrier.DeactivateBarrier();
                else Managers.GuardManager.Instance.securityBarrier.ActivateBarrier();
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
            case InteractionPoint.InteractionType.TutorialTakeDoc:
                directorAvatar.StartCoroutine(directorAvatar.TutorialAutoTourRoutine(Managers.TutorialBureaucracyQuest.Instance));
                break;
        }
        
        // Сразу обновляем кнопку после нажатия
        UpdateContextButton();
    }
}
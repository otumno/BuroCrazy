using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class DirectorControlPanelUI : MonoBehaviour
{
    [Header("Кнопки управления")]
    [SerializeField] private Button registrarButton;
    [SerializeField] private Button office1Button;
    [SerializeField] private Button office2Button;
    [SerializeField] private Button cashierButton;
    [SerializeField] private Button doorButton;

    [Header("Кнопки документов")]
    [SerializeField] private Button registrarDocsButton;
    [SerializeField] private Button office1DocsButton;
    [SerializeField] private Button office2DocsButton;
    [SerializeField] private Button cashierDocsButton;
    [SerializeField] private Button guardPostDocsButton;

    [Header("Ссылки на Service Points (Перетащить со сцены)")]
    [SerializeField] private List<ServicePoint> registrarPoints;
    [SerializeField] private List<ServicePoint> office1Points;
    [SerializeField] private List<ServicePoint> office2Points;
    [SerializeField] private List<ServicePoint> cashierPoints;
    [SerializeField] private ServicePoint guardReportDesk;

    void Start()
    {
        // Назначаем обработчики кнопок "работать"
        registrarButton?.onClick.AddListener(() => SendDirectorToWork(registrarPoints));
        office1Button?.onClick.AddListener(() => SendDirectorToWork(office1Points));
        office2Button?.onClick.AddListener(() => SendDirectorToWork(office2Points));
        cashierButton?.onClick.AddListener(() => SendDirectorToWork(cashierPoints));
        doorButton?.onClick.AddListener(GoAndToggleBarrier);

        // Назначаем обработчики кнопок "собрать документы"
        registrarDocsButton?.onClick.AddListener(() => SendDirectorToCollect(registrarPoints));
        office1DocsButton?.onClick.AddListener(() => SendDirectorToCollect(office1Points));
        office2DocsButton?.onClick.AddListener(() => SendDirectorToCollect(office2Points));
        cashierDocsButton?.onClick.AddListener(() => SendDirectorToCollect(cashierPoints));
        guardPostDocsButton?.onClick.AddListener(() => SendDirectorToCollect(new List<ServicePoint> { guardReportDesk }));
    }

    void Update()
    {
        bool directorIsBusy = DirectorAvatarController.Instance == null || DirectorAvatarController.Instance.IsInUninterruptibleAction;

        // Обновляем состояние кнопок "работать"
        UpdateWorkButton(registrarButton, registrarPoints, directorIsBusy);
        UpdateWorkButton(office1Button, office1Points, directorIsBusy);
        UpdateWorkButton(office2Button, office2Points, directorIsBusy);
        UpdateWorkButton(cashierButton, cashierPoints, directorIsBusy);
        doorButton.interactable = !directorIsBusy;

        // Обновляем кнопки "собрать документы"
        UpdateDocButton(registrarDocsButton, registrarPoints, directorIsBusy);
        UpdateDocButton(office1DocsButton, office1Points, directorIsBusy);
        UpdateDocButton(office2DocsButton, office2Points, directorIsBusy);
        UpdateDocButton(cashierDocsButton, cashierPoints, directorIsBusy);
        UpdateDocButton(guardPostDocsButton, new List<ServicePoint> { guardReportDesk }, directorIsBusy);
    }

    #region Вспомогательные методы

    private void UpdateWorkButton(Button button, List<ServicePoint> points, bool directorIsBusy)
    {
        if (button == null || points == null) return;

        bool hasFreeStation = points.Any(p => p != null && ClientSpawner.GetServiceProviderAtDesk(p.deskId) == null);
        button.gameObject.SetActive(true);
        button.interactable = !directorIsBusy && hasFreeStation;
    }

    private void UpdateDocButton(Button button, List<ServicePoint> points, bool directorIsBusy)
    {
        if (button == null || points == null)
        {
            if (button != null) button.gameObject.SetActive(false);
            return;
        }

        // Проверяем, есть ли хотя бы один стек с документами
        bool hasDocs = false;
        foreach (var point in points)
        {
            if (point != null && point.documentStack != null && !point.documentStack.IsEmpty)
            {
                hasDocs = true;
                break;
            }
        }

        button.gameObject.SetActive(hasDocs);
        button.interactable = !directorIsBusy && hasDocs;
    }

    private void SendDirectorToWork(List<ServicePoint> points)
    {
        if (DirectorAvatarController.Instance == null) return;
        if (points == null || points.Count == 0)
        {
            Debug.LogError("Для этой роли не назначено ни одного ServicePoint в инспекторе DirectorControlPanelUI!", this);
            return;
        }

        var unoccupiedPoints = points.Where(p => p != null && ClientSpawner.GetServiceProviderAtDesk(p.deskId) == null).ToList();
        if (unoccupiedPoints.Count == 0)
        {
            DirectorAvatarController.Instance.GetComponent<ThoughtBubbleController>()?.ShowPriorityMessage("Все места заняты!", 2f, Color.yellow);
            Debug.Log("Директор не может занять место, все станции этой группы заняты.");
            return;
        }

        var targetPoint = unoccupiedPoints[Random.Range(0, unoccupiedPoints.Count)];
        DirectorAvatarController.Instance.StartWorkingAt(targetPoint);
    }

    private void SendDirectorToCollect(List<ServicePoint> points)
    {
        if (DirectorAvatarController.Instance == null) return;
        if (points == null || points.Count == 0)
        {
            Debug.LogError("Нет точек для сбора документов.", this);
            return;
        }

        // Ищем первую точку с документами
        ServicePoint pointWithDocs = null;
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (p != null && p.documentStack != null && !p.documentStack.IsEmpty)
            {
                pointWithDocs = p;
                break;
            }
        }

        if (pointWithDocs == null)
        {
            Debug.Log("[Collect] Нет документов для сбора в указанной зоне.");
            return;
        }

        // Отправляем директора собирать документы
        DirectorAvatarController.Instance.CollectDocuments(pointWithDocs.documentStack);
        Debug.Log($"[Collect] Директор отправлен для сбора документов: {pointWithDocs.name}");
    }

    private void GoAndToggleBarrier()
    {
        if (DirectorAvatarController.Instance != null)
        {
            DirectorAvatarController.Instance.GoAndOperateBarrier();
        }
    }

    #endregion
}
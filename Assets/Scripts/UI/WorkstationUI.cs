// Файл: WorkstationUI.cs - НОВАЯ ВЕРСЯ
using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Managers;

public class WorkstationUI : MonoBehaviour
{
    // Шаг 1: Определяем типы станций, которые мы можем отслеживать
    [Header("Настройки Станции")]
    [Tooltip("Выберите тип станции, за которой будет следить этот UI элемент")]
    [SerializeField] private WorkstationType stationType;

    [Header("UI Компоненты")]
    [Tooltip("Текстовое поле для вывода статуса(ов)")]
    [SerializeField] private TextMeshProUGUI statusText;

    // Внутренние переменные для хранения найденных станций и сотрудников
    private List<ServicePoint> trackedServicePoints = new List<ServicePoint>();
    private float nextRefresh;
    private StringBuilder sb = new StringBuilder();

    // --- НОВЫЕ ПОЛЯ ДЛЯ JUICY ЭФФЕКТА ---
    private string lastStatusString = "";
    private SmartRoomLabel smartLabel;

    private void Start()
    {
        if (statusText == null) statusText = GetComponentInChildren<TextMeshProUGUI>();
        if (statusText == null)
        {
            Debug.LogError($"На объекте {gameObject.name} отсутствует TextMeshProUGUI!", gameObject);
            enabled = false;
            return;
        }

        // Ищем SmartRoomLabel на этом же объекте или у родителей
        smartLabel = GetComponentInParent<SmartRoomLabel>();

        // При старте находим нужные нам объекты на сцене
        FindTrackedObjects();
    }

    private void FindTrackedObjects()
    {
        if (ScenePointsRegistry.Instance == null)
            return;

        // В зависимости от типа, находим соответствующие ServicePoint'ы
        switch (stationType)
        {
            case WorkstationType.Registration:
                trackedServicePoints.AddRange(ScenePointsRegistry.Instance.allServicePoints.Where(p => p != null && p.deskId == 0 && p.clientStandPoint != null));
                break;
            case WorkstationType.Cashier:
                trackedServicePoints.Add(ScenePointsRegistry.Instance.GetServicePointByID(-1));
                break;
            case WorkstationType.OfficeDesk1:
                trackedServicePoints.Add(ScenePointsRegistry.Instance.GetServicePointByID(1));
                break;
            case WorkstationType.OfficeDesk2:
                trackedServicePoints.Add(ScenePointsRegistry.Instance.GetServicePointByID(2));
                break;
            case WorkstationType.GuardPost:
                break;
        }
        
        // Убираем из списка пустые/ненайденные точки
        trackedServicePoints.RemoveAll(item => item == null);
    }

    private void Update()
    {
        // ИСПРАВЛЕНИЕ: Убрали проверку на паузу. UI должен обновляться всегда!
        
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.2f;
        sb.Clear(); // Очищаем построитель строк перед новым циклом

        if (stationType == WorkstationType.GuardPost)
        {
            UpdateGuardPostStatus();
        }
        else
        {
            UpdateServicePointStatus();
        }

        string newStatus = sb.ToString();

        // --- ЛОГИКА "ПИНГА" ПРИ ИЗМЕНЕНИИ ---
        if (newStatus != lastStatusString)
        {
            // Не пингуем при самой первой инициализации (когда lastStatusString пустой)
            if (!string.IsNullOrEmpty(lastStatusString) && smartLabel != null)
            {
                smartLabel.Ping(2.5f); // Показываем надпись на 2.5 секунды
            }
            
            lastStatusString = newStatus;
            statusText.text = newStatus;
        }
    }

    private void UpdateGuardPostStatus()
    {
        var onDutyGuards = HiringManager.Instance?.AllStaff.OfType<GuardMovement>().Where(g => g != null && g.isActiveAndEnabled && g.IsOnDuty()).ToList();
        
        sb.AppendLine(UI.GameText.Get("room.guard_post"));

        if (onDutyGuards == null || onDutyGuards.Count == 0)
        {
            statusText.color = Color.red;
            sb.Append(UI.GameText.Get("room.nobody"));
        }
        else
        {
            statusText.color = Color.white;
            foreach (var guard in onDutyGuards)
            {
                string status = GetGuardStatusText(guard.GetCurrentState());
                sb.AppendLine($"{guard.characterName}: {status}");
            }
        }
    }

    private void UpdateServicePointStatus()
    {
        int activeStaffCount = 0;
        
        foreach (var point in trackedServicePoints)
        {
            IServiceProvider provider = ClientSpawner.GetServiceProviderAtDesk(point);
            if (provider != null)
            {
                activeStaffCount++;
                string staffName = (provider as MonoBehaviour)?.name ?? UI.GameText.Get("room.unknown");
                string status = UI.GameText.Get("room.free");
                Color statusColor = Color.green;

                // Если это клерк и он на перерыве
                if (provider is ClerkController clerk && clerk.IsOnBreak())
                {
                    status = UI.GameText.Get("room.break");
                    statusColor = Color.yellow;
                }
                // Если к столу привязан клиент
                else if (point.CurrentClient != null)
                {
                    int qNum = point.CurrentClient.stateMachine.MyQueueNumber;
                    // Отсекаем наглецов (номера 10000+)
                    string numStr = (qNum >= 10000 || qNum == -1) ? UI.GameText.Get("room.walk_in") : $"№{qNum}";
        
                    // Проверяем, подошел ли клиент физически к столу
                    var cState = point.CurrentClient.stateMachine.GetCurrentState();
                    bool isAtDesk = cState == ClientState.AtRegistration || cState == ClientState.AtDesk1 ||
                                    cState == ClientState.AtDesk2 || cState == ClientState.AtCashier ||
                                    cState == ClientState.InsideLimitedZone;
                        
                    if (isAtDesk)
                    {
                        status = UI.GameText.Get("room.serving",numStr);
                        statusColor = Color.cyan;
                    }
                    else
                    {
                        status = UI.GameText.Get("room.calling", numStr);
                        statusColor = new Color(1f, 0.6f, 0f); // Оранжевый
                    }
                }
                // Директор
                else if (provider is DirectorAvatarController director)
                {
                    if (director.GetCurrentState() == DirectorAvatarController.DirectorState.ServingClient)
                    {
                        status = UI.GameText.Get("room.busy");
                        statusColor = Color.cyan;
                    }
                }
    
                sb.AppendLine($"{staffName}: <color=#{ColorUtility.ToHtmlStringRGB(statusColor)}>{status}</color>");
            }
        }

        if (activeStaffCount == 0)
        {
            statusText.color = Color.red;
            sb.Append(UI.GameText.Get("room.vacant"));
        }
        else
        {
            statusText.color = Color.white;
        }
    }

    // Вспомогательные методы для текста статусов (можно расширять)
    private static string GetGuardStatusText(GuardMovement.GuardState state)
    {
        switch (state)
        {
            case GuardMovement.GuardState.Patrolling:
                return UI.GameText.Get("room.patrol");
            case GuardMovement.GuardState.OnPost:
                return UI.GameText.Get("room.on_post");
            case GuardMovement.GuardState.Chasing:
            case GuardMovement.GuardState.Talking:
            case GuardMovement.GuardState.ChasingThief:
                return UI.GameText.Get("room.intervening");
            case GuardMovement.GuardState.OnBreak:
                return UI.GameText.Get("room.lunch");
            case GuardMovement.GuardState.AtToilet:
                return UI.GameText.Get("room.toilet");
            case GuardMovement.GuardState.WritingReport:
                return UI.GameText.Get("room.report");
            default:
                return UI.GameText.Get("room.idle");
        }
    }
}
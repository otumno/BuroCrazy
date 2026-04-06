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
    private List<GuardMovement> allGuards = new List<GuardMovement>();
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
                trackedServicePoints.Add(ScenePointsRegistry.Instance.GetServicePointByID(0));
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
                // Для охраны мы просто находим всех охранников
                if (HiringManager.Instance != null)
                {
                    allGuards = HiringManager.Instance.AllStaff.OfType<GuardMovement>().ToList();
                }
                break;
        }
        
        // Убираем из списка пустые/ненайденные точки
        trackedServicePoints.RemoveAll(item => item == null);
    }

    private void Update()
    {
        // ИСПРАВЛЕНИЕ: Убрали проверку на паузу. UI должен обновляться всегда!
        
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
        var onDutyGuards = allGuards.Where(g => g != null && g.IsOnDuty()).ToList();
        
        sb.AppendLine("ПОСТ ОХРАНЫ");

        if (onDutyGuards.Count == 0)
        {
            statusText.color = Color.red;
            sb.Append("Никого нет на смене");
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
            IServiceProvider provider = ClientSpawner.GetServiceProviderAtDesk(point.deskId);
            if (provider != null)
            {
                activeStaffCount++;
                string staffName = (provider as MonoBehaviour)?.name ?? "Неизвестно";
                string status = "Свободна";
                Color statusColor = Color.green;

                // Если это клерк и он на перерыве
                if (provider is ClerkController clerk && clerk.IsOnBreak())
                {
                    status = "Перерыв";
                    statusColor = Color.yellow;
                }
                // Если к столу привязан клиент
                else if (point.CurrentClient != null)
                {
                    int qNum = point.CurrentClient.stateMachine.MyQueueNumber;
                    // Отсекаем наглецов (номера 10000+)
                    string numStr = (qNum >= 10000 || qNum == -1) ? "Вне очереди" : $"№{qNum}";
        
                    // Проверяем, подошел ли клиент физически к столу
                    var cState = point.CurrentClient.stateMachine.GetCurrentState();
                    bool isAtDesk = cState == ClientState.AtRegistration || cState == ClientState.AtDesk1 ||
                                    cState == ClientState.AtDesk2 || cState == ClientState.AtCashier ||
                                    cState == ClientState.InsideLimitedZone;
                        
                    if (isAtDesk)
                    {
                        status = $"Обслуживает {numStr}";
                        statusColor = Color.cyan;
                    }
                    else
                    {
                        status = $"Вызывает {numStr}";
                        statusColor = new Color(1f, 0.6f, 0f); // Оранжевый
                    }
                }
                // Директор
                else if (provider is DirectorAvatarController director)
                {
                    if (director.GetCurrentState() == DirectorAvatarController.DirectorState.ServingClient)
                    {
                        status = "Занят";
                        statusColor = Color.cyan;
                    }
                }
    
                sb.AppendLine($"{staffName}: <color=#{ColorUtility.ToHtmlStringRGB(statusColor)}>{status}</color>");
            }
        }

        if (activeStaffCount == 0)
        {
            statusText.color = Color.red;
            sb.Append("Не назначен");
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
                return "Патруль";
            case GuardMovement.GuardState.OnPost:
                return "На посту";
            case GuardMovement.GuardState.Chasing:
            case GuardMovement.GuardState.Talking:
            case GuardMovement.GuardState.ChasingThief:
                return "Разбирается";
            case GuardMovement.GuardState.OnBreak:
                return "Обед";
            case GuardMovement.GuardState.AtToilet:
                return "Перерыв";
            case GuardMovement.GuardState.WritingReport:
                return "Пишет отчет";
            default:
                return "Бездействует";
        }
    }
}
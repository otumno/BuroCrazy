// Файл: WorkstationUI.cs - НОВАЯ ВЕРСЯ
using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Text;
using System.Linq;

public class WorkstationUI : MonoBehaviour
{
    // Шаг 1: Определяем типы станций, которые мы можем отслеживать
    public enum WorkstationType
    {
        Registrar,
        Cashier,
        OfficeDesk1,
        OfficeDesk2,
        GuardPost
    }

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

    void Start()
    {
        if (statusText == null) statusText = GetComponentInChildren<TextMeshProUGUI>();
        if (statusText == null)
        {
            Debug.LogError($"На объекте {gameObject.name} отсутствует TextMeshProUGUI!", gameObject);
            enabled = false;
            return;
        }

        // При старте находим нужные нам объекты на сцене
        FindTrackedObjects();
    }

    void FindTrackedObjects()
    {
        if (ScenePointsRegistry.Instance == null) return;

        // В зависимости от типа, находим соответствующие ServicePoint'ы
        switch (stationType)
        {
            case WorkstationType.Registrar:
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

    void Update()
    {
        if (Time.timeScale == 0f) return;

        sb.Clear(); // Очищаем построитель строк перед новым циклом

        if (stationType == WorkstationType.GuardPost)
        {
            UpdateGuardPostStatus();
        }
        else
        {
            UpdateServicePointStatus();
        }

        statusText.text = sb.ToString();
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
                string status = "Работает";
                Color statusColor = Color.green;

                // Определяем статус в зависимости от типа сотрудника
                if (provider is ClerkController clerk)
                {
                    if (clerk.IsOnBreak())
                    {
                        status = "Перерыв";
                        statusColor = Color.yellow;
                    }
                } 
                else if (provider is DirectorAvatarController director)
                {
                    if (director.GetCurrentState() == DirectorAvatarController.DirectorState.ServingClient)
                    {
                        status = "Обслуживает";
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
    private string GetGuardStatusText(GuardMovement.GuardState state)
    {
        switch (state)
        {
            case GuardMovement.GuardState.Patrolling: return "Патруль";
            case GuardMovement.GuardState.OnPost: return "На посту";
            case GuardMovement.GuardState.Chasing:
            case GuardMovement.GuardState.Talking:
            case GuardMovement.GuardState.ChasingThief:
                return "Разбирается";
            case GuardMovement.GuardState.OnBreak: return "Обед";
            case GuardMovement.GuardState.AtToilet: return "Перерыв";
            case GuardMovement.GuardState.WritingReport: return "Пишет отчет";
            default: return "Бездействует";
        }
    }
}
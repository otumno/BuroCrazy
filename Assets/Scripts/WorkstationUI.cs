// Файл: WorkstationUI.cs
using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Text;

public class WorkstationUI : MonoBehaviour
{
    [Header("Режим отслеживания")]
    [Tooltip("(Для одного персонажа) Перетащите сюда объект Клерка или Охранника")]
    public MonoBehaviour trackedCharacter;
    [Tooltip("(Для группы) Перетащите сюда ВСЕХ охранников со сцены")]
    public List<GuardMovement> trackedGuards;
    
    [Header("UI Компоненты")]
    [Tooltip("Основное текстовое поле для вывода статуса(ов)")]
    public TextMeshProUGUI statusText;

    private ClerkController clerk;
    private GuardMovement singleGuard;
    private StringBuilder sb = new StringBuilder();

    void Start()
    {
        if (statusText == null)
        {
            statusText = GetComponentInChildren<TextMeshProUGUI>();
        }

        if (statusText == null) 
        { 
            Debug.LogError("На объекте WorkstationUI или его дочерних объектах отсутствует компонент TextMeshProUGUI!", gameObject);
            enabled = false; 
            return; 
        }

        if (trackedCharacter != null)
        {
            clerk = trackedCharacter.GetComponent<ClerkController>();
            singleGuard = trackedCharacter.GetComponent<GuardMovement>();
        }
    }

    void Update()
    {
        if (Time.timeScale == 0f)
        {
            // Больше не пишем "Пауза" здесь, чтобы не перезатирать статусы
            return;
        }

        if (trackedGuards != null && trackedGuards.Count > 0)
        {
            UpdateAllGuardsStatus();
        }
        else if (trackedCharacter != null)
        {
            if (clerk != null) { UpdateClerkStatus(); }
            else if (singleGuard != null) { UpdateGuardStatus(singleGuard); }
        }
        else
        {
            statusText.text = "Никто не назначен";
        }
    }

    void UpdateAllGuardsStatus()
    {
        sb.Clear();
        for (int i = 0; i < trackedGuards.Count; i++)
        {
            if (trackedGuards[i] != null)
            {
                sb.Append($"Охранник {i + 1}: ");
                sb.Append(GetGuardStatusText(trackedGuards[i]));
                
                if (i < trackedGuards.Count - 1)
                {
                    sb.Append("\n");
                }
            }
        }
        statusText.text = sb.ToString();
        statusText.color = Color.white;
    }

    void UpdateClerkStatus()
    {
        var state = clerk.GetCurrentState();
        string period = ClientSpawner.CurrentPeriodName?.ToLower().Trim();
        switch (state)
        {
            case ClerkController.ClerkState.Working: 
                statusText.text = "На месте";
                statusText.color = Color.green; 
                break;
            case ClerkController.ClerkState.OnBreak: 
                if (period == "ночь") 
                { 
                    statusText.text = "Смена окончена"; 
                    statusText.color = Color.grey;
                } 
                else 
                { 
                    statusText.text = "Обед"; 
                    statusText.color = Color.yellow; 
                } 
                break;
            case ClerkController.ClerkState.AtToilet:
            case ClerkController.ClerkState.GoingToToilet:
                statusText.text = "Перерыв";
                statusText.color = Color.yellow; 
                break;
            default: 
                statusText.text = "Отсутствует"; 
                statusText.color = Color.red; 
                break;
        }
    }

    void UpdateGuardStatus(GuardMovement guard)
    {
        statusText.text = GetGuardStatusText(guard);
        statusText.color = GetGuardStatusColor(guard.GetCurrentState());
    }

    private string GetGuardStatusText(GuardMovement guard)
    {
        var state = guard.GetCurrentState();
        string period = ClientSpawner.CurrentPeriodName?.ToLower().Trim();

        switch (state)
        {
            case GuardMovement.GuardState.Patrolling: return "Патрулирование";
            case GuardMovement.GuardState.OnPost:
                if (period == "ночь") return "Ночное дежурство";
                return "На обеде";
            case GuardMovement.GuardState.GoingToToilet:
            case GuardMovement.GuardState.AtToilet:
                return "Перерыв";
            case GuardMovement.GuardState.Chasing:
            case GuardMovement.GuardState.Talking:
                return "ПРЕСЛЕДОВАНИЕ";
            case GuardMovement.GuardState.OffDuty:
                return "Смена окончена";
            default:
                return "Занят";
        }
    }

    private Color GetGuardStatusColor(GuardMovement.GuardState state)
    {
        switch (state)
        {
            case GuardMovement.GuardState.Patrolling: return Color.white;
            case GuardMovement.GuardState.OnPost: return Color.cyan;
            case GuardMovement.GuardState.GoingToToilet:
            case GuardMovement.GuardState.AtToilet:
                return Color.yellow;
            case GuardMovement.GuardState.Chasing:
            case GuardMovement.GuardState.Talking:
                return Color.red;
            default:
                return Color.grey;
        }
    }
}
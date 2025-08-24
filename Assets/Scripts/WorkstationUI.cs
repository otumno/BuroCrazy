using UnityEngine;
using TMPro;

public class WorkstationUI : MonoBehaviour
{
    [Tooltip("Перетащите сюда объект Клерка или Охранника, за которым нужно следить")]
    public MonoBehaviour trackedCharacter;
    
    private TextMeshProUGUI statusText;
    private ClerkController clerk;
    private GuardMovement guard;

    void Start()
    {
        statusText = GetComponent<TextMeshProUGUI>();
        if (statusText == null)
        {
            Debug.LogError("На объекте WorkstationUI отсутствует компонент TextMeshProUGUI!", gameObject);
            enabled = false;
            return;
        }

        if (trackedCharacter != null)
        {
            clerk = trackedCharacter.GetComponent<ClerkController>();
            guard = trackedCharacter.GetComponent<GuardMovement>();
        }
    }

    void Update()
    {
        if (trackedCharacter == null)
        {
            statusText.text = "Никто не назначен";
            return;
        }

        if (clerk != null)
        {
            UpdateClerkStatus();
        }
        else if (guard != null)
        {
            UpdateGuardStatus();
        }
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
                if (period == "ночь") { statusText.text = "Смена окончена"; statusText.color = Color.grey; }
                else { statusText.text = "Обед"; statusText.color = Color.yellow; }
                break;
            case ClerkController.ClerkState.AtToilet:
                statusText.text = "Перерыв";
                statusText.color = Color.yellow;
                break;
            default: // Все состояния движения
                statusText.text = "Отсутствует";
                statusText.color = Color.red;
                break;
        }
    }

    void UpdateGuardStatus()
    {
        var state = guard.GetCurrentState();
        string period = ClientSpawner.CurrentPeriodName?.ToLower().Trim();

        switch (state)
        {
            case GuardMovement.GuardState.Patrolling:
                statusText.text = "Патрулирование";
                statusText.color = Color.white;
                break;
            case GuardMovement.GuardState.OnPost:
                if (period == "ночь") { statusText.text = "Ночное дежурство"; statusText.color = Color.cyan; }
                else { statusText.text = "На обеде"; statusText.color = Color.yellow; }
                break;
            case GuardMovement.GuardState.Chasing:
            case GuardMovement.GuardState.Talking:
                statusText.text = "ПРЕСЛЕДОВАНИЕ";
                statusText.color = Color.red;
                break;
            default:
                statusText.text = "Занят";
                statusText.color = Color.grey;
                break;
        }
    }
}
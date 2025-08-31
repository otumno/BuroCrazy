using UnityEngine;
using System.Collections;
using TMPro;

public class ThoughtBubbleController : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Ссылка на ассет с коллекцией всех мыслей")]
    public ThoughtCollection thoughtCollection;

    [Header("Ссылки на компоненты (перетащить из иерархии префаба)")]
    [Tooltip("Объект 'ThoughtBubble', который включает фон и текст")]
    public GameObject thoughtBubbleObject;

    [Tooltip("Компонент TextMeshPro для отображения мысли")]
    public TextMeshPro thoughtTextMesh;
    
    [Header("Тайминги")]
    public float minThinkInterval = 10f;
    public float maxThinkInterval = 20f;
    public float thoughtDuration = 4f;

    // Ссылки на скрипты состояний
    private ClientPathfinding clientPathfinding;
    private ClerkController clerkController;
    private GuardMovement guardMovement;
    private InternController internController;
    
    private Coroutine thinkLoopCoroutine;

    void Awake()
    {
        clientPathfinding = GetComponent<ClientPathfinding>();
        clerkController = GetComponent<ClerkController>();
        guardMovement = GetComponent<GuardMovement>();
        internController = GetComponent<InternController>();
    }

    void Start()
    {
        if (thoughtCollection == null || thoughtBubbleObject == null || thoughtTextMesh == null)
        {
            Debug.LogError($"ThoughtBubbleController на {gameObject.name} не настроен. Отключаю.", gameObject);
            enabled = false;
            return;
        }
        
        thoughtBubbleObject.SetActive(false);
        thinkLoopCoroutine = StartCoroutine(ThinkLoop());
    }
    
    public void TriggerCriticalThought(string activityKey)
    {
        if (thoughtBubbleObject.activeSelf) return;

        string thoughtText = thoughtCollection.GetRandomThought(activityKey, 0f);
        if (!string.IsNullOrEmpty(thoughtText))
        {
            StartCoroutine(ShowThought(thoughtText, Color.red, true));
        }
    }

    public void StopThinking()
    {
        if (thinkLoopCoroutine != null)
        {
            StopCoroutine(thinkLoopCoroutine);
        }
    }

    private IEnumerator ThinkLoop()
    {
        while (true)
        {
            float waitTime = Random.Range(minThinkInterval, maxThinkInterval);
            yield return new WaitForSeconds(waitTime);

            if (thoughtBubbleObject.activeSelf) continue;

            (string activityKey, float parameterValue) = DetermineThoughtParameters();

            if (!string.IsNullOrEmpty(activityKey))
            {
                string thoughtText = thoughtCollection.GetRandomThought(activityKey, parameterValue);
                if (!string.IsNullOrEmpty(thoughtText))
                {
                    // --- ИЗМЕНЕНИЕ: Определяем цвет и передаем его дальше ---
                    Color thoughtColor = GetColorForParameter(parameterValue);
                    StartCoroutine(ShowThought(thoughtText, thoughtColor));
                }
            }
        }
    }

    // --- НОВЫЙ МЕТОД: Определяет цвет на основе параметра ---
    private Color GetColorForParameter(float parameterValue)
    {
        if (parameterValue >= 0.66f) return Color.green; // Зеленый для высокого терпения / низкого стресса
        if (parameterValue >= 0.33f) return Color.yellow; // Желтый для среднего
        return new Color(1.0f, 0.64f, 0.0f); // Оранжевый для низкого терпения / высокого стресса
    }


    private (string, float) DetermineThoughtParameters()
    {
        string key = "";
        float param = 1f;

        if (clientPathfinding != null)
        {
            ClientState state = clientPathfinding.stateMachine.GetCurrentState();
            
            switch (state)
            {
                case ClientState.AtWaitingArea:
                case ClientState.SittingInWaitingArea:
                case ClientState.AtLimitedZoneEntrance:
                    key = "Client_Waiting";
                    break;
                case ClientState.MovingToGoal:
                case ClientState.MovingToSeat:
                case ClientState.MovingToRegistrarImpolite:
                case ClientState.ReturningToWait:
                    Waypoint goal = clientPathfinding.stateMachine.GetCurrentGoal();
                    if (goal != null && ClientQueueManager.Instance.IsWaypointInWaitingZone(goal))
                        key = "Client_GoingToQueue";
                    else if (goal != null && goal.GetComponentInParent<LimitedCapacityZone>() == ClientSpawner.GetToiletZone())
                        key = "Client_GoingToToilet";
                    else
                        key = "Client_GoingToService";
                    break;
                case ClientState.Leaving:
                case ClientState.LeavingUpset:
                    key = (clientPathfinding.reasonForLeaving == ClientPathfinding.LeaveReason.Processed) 
                        ? "Client_Leaving_Happy" : "Client_Leaving_Unhappy";
                    break;
                case ClientState.AtRegistration:
                case ClientState.AtDesk1:
                case ClientState.AtDesk2:
                case ClientState.AtCashier:
                case ClientState.InsideLimitedZone:
                    key = "Client_Service";
                    break;
                case ClientState.AtToilet:
                    key = "Staff_OnBreak";
                    break;
                default:
                    key = $"Client_{state}";
                    break;
            }
            param = Random.Range(0.2f, 1f); // ЗАГЛУШКА
        }
        else if (clerkController != null)
        {
            var clerkState = clerkController.GetCurrentState();
            if (clerkState == ClerkController.ClerkState.StressedOut)
                key = "Staff_StressedOut";
            else if (clerkState == ClerkController.ClerkState.AtToilet || clerkState == ClerkController.ClerkState.OnBreak)
                key = "Staff_OnBreak";
            else
                key = "Staff_Working";
            
            param = 1f - clerkController.GetStressPercent();
        }
        else if (guardMovement != null)
        {
            var guardState = guardMovement.GetCurrentState();
            if (guardState == GuardMovement.GuardState.StressedOut)
                key = "Staff_StressedOut";
            else if (guardState == GuardMovement.GuardState.Chasing || guardState == GuardMovement.GuardState.Evicting)
                key = "Staff_Action";
            else if (guardState == GuardMovement.GuardState.AtToilet || guardState == GuardMovement.GuardState.OnBreak)
                key = "Staff_OnBreak";
            else
                key = "Staff_Working";

            param = 1f - guardMovement.GetStressPercent();
        }
        else if (internController != null)
        {
            var internState = internController.GetCurrentState();
            if (internState == InternController.InternState.HelpingConfused || internState == InternController.InternState.ServingFromQueue)
                 key = "Staff_Action";
            else if (internState == InternController.InternState.AtToilet || internState == InternController.InternState.OnBreak)
                key = "Staff_OnBreak";
            else
                key = "Staff_Working";
            
            param = Random.Range(0.2f, 1f); // ЗАГЛУШКА
        }
        
        return (key, param);
    }

    private IEnumerator ShowThought(string text, Color? textColor = null, bool allCaps = false)
    {
        thoughtTextMesh.text = allCaps ? text.ToUpper() : text;
        
        // --- ИЗМЕНЕНИЕ: Теперь мы всегда устанавливаем цвет ---
        if (textColor.HasValue)
        {
            thoughtTextMesh.color = textColor.Value;
        }
        else
        {
            thoughtTextMesh.color = Color.black; // Стандартный цвет, если никакой не передан
        }
        
        thoughtBubbleObject.SetActive(true);
        
        yield return new WaitForSeconds(thoughtDuration);

        thoughtBubbleObject.SetActive(false);
    }
}
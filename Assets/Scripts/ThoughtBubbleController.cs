// Файл: ThoughtBubbleController.cs
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

    private ClientPathfinding clientPathfinding;
    private ClerkController clerkController;
    private GuardMovement guardMovement;
    private InternController internController;

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
        StartCoroutine(ThinkLoop()); // Запускаем основной цикл один раз при старте
    }
    
    /// <summary>
    /// Показывает конкретное сообщение, прерывая любую текущую мысль.
    /// </summary>
    /// <param name="message">Текст для отображения</param>
    /// <param name="duration">Как долго показывать сообщение</param>
    /// <param name="textColor">Цвет текста</param>
    public void ShowPriorityMessage(string message, float duration = 3f, Color? textColor = null)
    {
        // Останавливаем ВСЕ корутины на этом скрипте
        // Это гарантирует, что старый цикл мыслей и другие сообщения будут прерваны
        StopAllCoroutines();
        
        // Если цвет не указан, используем черный. Иначе - тот, что передали.
        Color colorToShow = textColor ?? Color.black;
        // Запускаем показ нового сообщения
        StartCoroutine(ShowSingleMessageRoutine(message, duration, colorToShow));
    }

    /// <summary>
    /// Корутина, которая показывает одно сообщение, а затем перезапускает основной цикл мыслей.
    /// </summary>
    private IEnumerator ShowSingleMessageRoutine(string text, float duration, Color color)
    {
        thoughtTextMesh.text = text;
        thoughtTextMesh.color = color; // Используем переданный цвет
        thoughtBubbleObject.SetActive(true);

        yield return new WaitForSeconds(duration);

        thoughtBubbleObject.SetActive(false);

        // После того, как приоритетное сообщение было показано, перезапускаем основной цикл
        StartCoroutine(ThinkLoop());
    }


    private IEnumerator ThinkLoop()
    {
        while (true)
        {
            float waitTime = Random.Range(minThinkInterval, maxThinkInterval);
            yield return new WaitForSeconds(waitTime);

            (string activityKey, float parameterValue) = DetermineThoughtParameters();
            if (!string.IsNullOrEmpty(activityKey))
            {
                string thoughtText = thoughtCollection.GetRandomThought(activityKey, parameterValue);
                if (!string.IsNullOrEmpty(thoughtText))
                {
                    Color thoughtColor = GetColorForParameter(parameterValue);
                    // Запускаем показ как отдельную корутину, которая НЕ перезапускает главный цикл
                    StartCoroutine(ShowRandomThoughtRoutine(thoughtText, thoughtColor));
                }
            }
        }
    }

    /// <summary>
    /// Корутина для показа ОБЫЧНОЙ мысли. Она не перезапускает главный цикл.
    /// </summary>
    private IEnumerator ShowRandomThoughtRoutine(string text, Color color)
    {
        thoughtTextMesh.text = text;
        thoughtTextMesh.color = color;
        thoughtBubbleObject.SetActive(true);

        yield return new WaitForSeconds(thoughtDuration);
        
        thoughtBubbleObject.SetActive(false);
    }
    
    public void TriggerCriticalThought(string activityKey)
    {
        StopAllCoroutines();
        string thoughtText = thoughtCollection.GetRandomThought(activityKey, 0f);
        if (!string.IsNullOrEmpty(thoughtText))
        {
            // Используем ту же корутину, что и для приоритетных сообщений
            StartCoroutine(ShowSingleMessageRoutine(thoughtText.ToUpper(), thoughtDuration, Color.red));
        }
    }

    public void StopThinking()
    {
        StopAllCoroutines();
    }
    
    private Color GetColorForParameter(float parameterValue)
    {
        if (parameterValue >= 0.66f) return Color.green;
        if (parameterValue >= 0.33f) return Color.yellow;
        return new Color(1.0f, 0.64f, 0.0f);
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
            param = Random.Range(0.2f, 1f);
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
            param = Random.Range(0.2f, 1f);
        }
        
        return (key, param);
    }
}
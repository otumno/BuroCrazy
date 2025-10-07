using UnityEngine;
using System.Collections;
using TMPro;

public class ThoughtBubbleController : MonoBehaviour
{
    [Header("Настройки")]
    public ThoughtCollection thoughtCollection;
    [Header("Ссылки на компоненты")]
    public GameObject thoughtBubbleObject;
    public TextMeshPro thoughtTextMesh;
    
    [Header("Тайминги")]
    public float minThinkInterval = 10f;
    public float maxThinkInterval = 20f;
    public float thoughtDuration = 4f;

    // --- ДОБАВЛЕНА ССЫЛКА НА ДИРЕКТОРА ---
    private DirectorAvatarController directorController;
    private ClientPathfinding clientPathfinding;
    private ClerkController clerkController;
    private GuardMovement guardMovement;
    private InternController internController;
    private ServiceWorkerController serviceWorkerController; // Добавим и для уборщика на будущее

    void Awake()
    {
        directorController = GetComponent<DirectorAvatarController>();
        clientPathfinding = GetComponent<ClientPathfinding>();
        clerkController = GetComponent<ClerkController>();
        guardMovement = GetComponent<GuardMovement>();
        internController = GetComponent<InternController>();
        serviceWorkerController = GetComponent<ServiceWorkerController>();
    }

    void Start()
    {
        if (thoughtCollection == null || thoughtBubbleObject == null || thoughtTextMesh == null)
        {
            enabled = false;
            return;
        }
        
        thoughtBubbleObject.SetActive(false);
        StartCoroutine(ThinkLoop());
    }
    
    // ... (методы ShowPriorityMessage, ShowSingleMessageRoutine, ThinkLoop, ShowRandomThoughtRoutine, TriggerCriticalThought, StopThinking, GetColorForParameter остаются без изменений) ...
    public void ShowPriorityMessage(string message, float duration = 3f, Color? textColor = null) { StopAllCoroutines(); Color colorToShow = textColor ?? Color.white; StartCoroutine(ShowSingleMessageRoutine(message, duration, colorToShow)); }
    private IEnumerator ShowSingleMessageRoutine(string text, float duration, Color color) { thoughtTextMesh.text = text; thoughtTextMesh.color = color; thoughtBubbleObject.SetActive(true); yield return new WaitForSeconds(duration); thoughtBubbleObject.SetActive(false); StartCoroutine(ThinkLoop()); }
    private IEnumerator ThinkLoop() { while (true) { float waitTime = Random.Range(minThinkInterval, maxThinkInterval); yield return new WaitForSeconds(waitTime); (string activityKey, float parameterValue) = DetermineThoughtParameters(); if (!string.IsNullOrEmpty(activityKey)) { string thoughtText = thoughtCollection.GetRandomThought(activityKey, parameterValue); if (!string.IsNullOrEmpty(thoughtText)) { Color thoughtColor = GetColorForParameter(parameterValue); StartCoroutine(ShowRandomThoughtRoutine(thoughtText, thoughtColor)); } } } }
    private IEnumerator ShowRandomThoughtRoutine(string text, Color color) { thoughtTextMesh.text = text; thoughtTextMesh.color = color; thoughtBubbleObject.SetActive(true); yield return new WaitForSeconds(thoughtDuration); thoughtBubbleObject.SetActive(false); }
    public void TriggerCriticalThought(string activityKey) { StopAllCoroutines(); string thoughtText = thoughtCollection.GetRandomThought(activityKey, 0f); if (!string.IsNullOrEmpty(thoughtText)) { StartCoroutine(ShowSingleMessageRoutine(thoughtText.ToUpper(), thoughtDuration, Color.red)); } }
    public void StopThinking() { StopAllCoroutines(); }
    private Color GetColorForParameter(float parameterValue) { if (parameterValue >= 0.66f) return Color.green; if (parameterValue >= 0.33f) return Color.yellow; return new Color(1.0f, 0.64f, 0.0f); }
    
    // --- ПОЛНОСТЬЮ ПЕРЕПИСАННЫЙ МЕТОД ---
    private (string, float) DetermineThoughtParameters()
    {
        string key = "";
        float param = 1f; // 1.0 = хорошо, 0.0 = плохо

        if (clientPathfinding != null)
        {
            var state = clientPathfinding.stateMachine.GetCurrentState();
            if (state == ClientState.SittingInWaitingArea || state == ClientState.AtWaitingArea)
            {
                key = "Client_Waiting";
                // Здесь можно будет привязать параметр к реальному терпению клиента
            }
        }
        else if (guardMovement != null)
        {
            var state = guardMovement.GetCurrentState();
            if (state == GuardMovement.GuardState.Chasing || state == GuardMovement.GuardState.Talking)
            {
                key = "Staff_Action"; // Ключ для активных действий
            }
            else if (state == GuardMovement.GuardState.OnBreak || state == GuardMovement.GuardState.AtToilet)
            {
                key = "Staff_OnBreak";
            }
            else if(state == GuardMovement.GuardState.Patrolling || state == GuardMovement.GuardState.WritingReport)
            {
                key = "Staff_Working";
            }
            param = 1f - guardMovement.GetCurrentFrustration(); // Чем выше выгорание, тем хуже мысли
        }
        else if (directorController != null)
        {
            var state = directorController.GetCurrentState();
            if(state == DirectorAvatarController.DirectorState.AtDesk)
            {
                 key = "Director_Working";
            }
            else if(state == DirectorAvatarController.DirectorState.Idle || state == DirectorAvatarController.DirectorState.MovingToPoint)
            {
                key = "Director_Idle";
            }
        }
        // Добавьте сюда else if для clerkController, internController, serviceWorkerController по аналогии...
        
        return (key, param);
    }
}
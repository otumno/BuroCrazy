using UnityEngine;
using System.Collections;
using TMPro;
using Managers;
using Scriptables.Audio;

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

    // --- Ссылки на контроллеры (для определения состояния и голоса) ---
    private StaffController myStaffController;
    private DirectorAvatarController directorController;
    private ClientPathfinding clientPathfinding;
    private ClerkController clerkController;
    private GuardMovement guardMovement;
    private InternController internController;
    private ServiceWorkerController serviceWorkerController;

    // --- Переменная для контроля корутины голоса ---
    private Coroutine talkingCoroutine;

    // т.к. это всё в 1 сцене по сути, то референсы можно сериализовать просто в 1 поле
    private void Awake()
    {
        // Кэшируем ссылки на все возможные контроллеры на этом объекте
        myStaffController = GetComponent<StaffController>();
        directorController = GetComponent<DirectorAvatarController>();
        clientPathfinding = GetComponent<ClientPathfinding>();
        clerkController = GetComponent<ClerkController>();
        guardMovement = GetComponent<GuardMovement>();
        internController = GetComponent<InternController>();
        serviceWorkerController = GetComponent<ServiceWorkerController>();
    }

    private void Start()
    {
        if (thoughtCollection == null || thoughtBubbleObject == null || thoughtTextMesh == null)
        {
            // Если обязательные ссылки не настроены, отключаем скрипт, чтобы не спамил ошибками
            enabled = false;
            return;
        }
        
        thoughtBubbleObject.SetActive(false);
        StartCoroutine(ThinkLoop());
    }

    // --- Метод для показа ОДИНОЧНОГО сообщения (с озвучкой) ---
    private IEnumerator ShowSingleMessageRoutine(string text, float duration, Color color)
    {
        thoughtTextMesh.text = text;
        thoughtTextMesh.color = color;
        thoughtBubbleObject.SetActive(true);

        // >>> ЗАПУСК ОЗВУЧКИ <<<
        // Если уже говорим, прерываем предыдущую фразу
        if (talkingCoroutine != null) StopCoroutine(talkingCoroutine);
        talkingCoroutine = StartCoroutine(PlayVoiceForText(text));
        // >>> КОНЕЦ <<<

        yield return new WaitForSeconds(duration);
        
        thoughtBubbleObject.SetActive(false);
        
        // Возвращаемся к циклу случайных мыслей
        StartCoroutine(ThinkLoop()); 
    }

    // --- Метод для показа СЛУЧАЙНОЙ мысли из цикла (с озвучкой) ---
    private IEnumerator ShowRandomThoughtRoutine(string text, Color color)
    {
        thoughtTextMesh.text = text;
        thoughtTextMesh.color = color;
        thoughtBubbleObject.SetActive(true);

        // >>> ЗАПУСК ОЗВУЧКИ <<<
        if (talkingCoroutine != null) StopCoroutine(talkingCoroutine);
        talkingCoroutine = StartCoroutine(PlayVoiceForText(text));
        // >>> КОНЕЦ <<<

        yield return new WaitForSeconds(thoughtDuration);
        thoughtBubbleObject.SetActive(false);
    }

    // --- ГЛАВНАЯ КОРУТИНА ОЗВУЧКИ (Gibberish System) ---
    private IEnumerator PlayVoiceForText(string text)
    {
        VoiceData voice = null;

        // дублирующиеся данные - хуёво
        if (myStaffController != null)
            voice = myStaffController.voiceProfile;
        else if (clientPathfinding != null)
            voice = clientPathfinding.voiceProfile;

        if (voice == null || voice.speechClips == null || voice.speechClips.Count == 0)
            yield break;

        // 2. Разбиваем текст на слова
        string[] words = text.Split(' ');

        // 3. Читаем каждое слово
        foreach (string word in words)
        {
            if (string.IsNullOrWhiteSpace(word))
                continue;

            // --- ЛОГИКА СЛОГОВ ---
            int soundsToPlay = 1; // По умолчанию 1 звук (для 1 буквы)

            if (word.Length > 1)
            {
                if (word.Length < 5) soundsToPlay = 2; // Среднее слово
                else soundsToPlay = 3; // Длинное слово
            }
            // ---------------------

            // Играем серию звуков для одного слова
            for (int i = 0; i < soundsToPlay; i++)
            {
                AudioClip clip = voice.GetRandomClip();
            
                if (AudioManager.Instance != null && clip != null)
                {
                    AudioManager.Instance.PlayVoiceClip(clip,
                        transform.position, 
                        voice.basePitch, 
                        voice.pitchDelta, 
                        voice.volume
                    );
                }

                // Маленькая пауза между слогами (бип...бип)
                // Если это последний слог в слове, паузу здесь можно не делать, 
                // но лучше сделать для четкости.
                if (i < soundsToPlay - 1)
                {
                    yield return new WaitForSeconds(voice.delayPerSyllable);
                }
            }

            // Ждем перед следующим словом (Пауза между словами)
            yield return new WaitForSeconds(voice.delayPerWord);
        }
    }

    // --- ПУБЛИЧНЫЙ API ---

    // Вызвать важное сообщение (например, "Взятка!", "Туалет!")
    public void ShowPriorityMessage(string message, float duration = 3f, Color? textColor = null) 
    { 
        StopAllCoroutines(); 
        Color colorToShow = textColor ?? Color.white; 
        StartCoroutine(ShowSingleMessageRoutine(message, duration, colorToShow)); 
    }

    // Вызвать критическую мысль (например, при ярости)
    public void TriggerCriticalThought(string activityKey) 
    { 
        StopAllCoroutines(); 
        string thoughtText = thoughtCollection.GetRandomThought(activityKey, 0f); 
        if (!string.IsNullOrEmpty(thoughtText)) 
        { 
            StartCoroutine(ShowSingleMessageRoutine(thoughtText.ToUpper(), thoughtDuration, Color.red)); 
        } 
    }

    // Принудительно заткнуть персонажа (и убрать бабл)
    public void StopThinking() 
    { 
        StopAllCoroutines(); 
        if(talkingCoroutine != null) StopCoroutine(talkingCoroutine); // Останавливаем звук
        thoughtBubbleObject.SetActive(false);
    }
    
    // --- ВНУТРЕННЯЯ ЛОГИКА ---

    // Бесконечный цикл генерации фоновых мыслей
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
                    StartCoroutine(ShowRandomThoughtRoutine(thoughtText, thoughtColor)); 
                } 
            } 
        } 
    }

    private Color GetColorForParameter(float parameterValue) 
    { 
        if (parameterValue >= 0.66f) return Color.green; 
        if (parameterValue >= 0.33f) return Color.yellow; 
        return new Color(1.0f, 0.64f, 0.0f); 
    }

    // не надо пользоваться строками, которые прописаны прямо в коде либо енум заводи, либо пиши данные отдельно в конфиг
    private (string, float) DetermineThoughtParameters()
    {
        string key = "";
        float param = 1f;

        if (clientPathfinding != null)
        {
            var state = clientPathfinding.stateMachine.GetCurrentState();
            if (state == ClientState.SittingInWaitingArea || state == ClientState.AtWaitingArea)
            {
                key = "Client_Waiting";
            }
            // Можно добавить другие состояния клиента
        }
        else if (guardMovement != null)
        {
            var state = guardMovement.GetCurrentState();
            if (state == GuardMovement.GuardState.Chasing || state == GuardMovement.GuardState.Talking) key = "Staff_Action";
            else if (state == GuardMovement.GuardState.OnBreak || state == GuardMovement.GuardState.AtToilet) key = "Staff_OnBreak";
            else if (state == GuardMovement.GuardState.Patrolling || state == GuardMovement.GuardState.WritingReport) key = "Staff_Working";
            param = 1f - guardMovement.GetCurrentFrustration();
        }
        else if (directorController != null)
        {
            var state = directorController.GetCurrentState();
            if (state == DirectorAvatarController.DirectorState.AtDesk) key = "Director_Working";
            else if (state == DirectorAvatarController.DirectorState.Idle || state == DirectorAvatarController.DirectorState.MovingToPoint) key = "Director_Idle";
        }
        else if (clerkController != null)
        {
             var state = clerkController.GetCurrentState();
             if (state == ClerkController.ClerkState.Working) key = "Staff_Working";
             else if (state == ClerkController.ClerkState.OnBreak) key = "Staff_OnBreak";
             param = 1f - clerkController.GetCurrentFrustration();
        }
        // Можно добавить internController и serviceWorkerController по аналогии
        
        return (key, param);
    }
}
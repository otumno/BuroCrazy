using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Managers;
using Utilities;
using Enums;
using Characters;
using Data;

public class TemporaryClownAI : MonoBehaviour
{
    public enum ClownMode { Clients, Staff }
    public ClownMode Mode => mode;
    [SerializeField] private ClownMode mode = ClownMode.Clients;

    public void SetMode(ClownMode newMode) => mode = newMode;
    [SerializeField] private float stressReductionRate = 3f;
    [SerializeField] private float lifetime = 60f;
    [SerializeField] private float jokeInterval = 8f;
    [SerializeField] private float reactionChance = 0.3f;

    [Header("NPC Data")]
    public Data.TemporaryNPCData npcData;

    private AgentMover mover;
    private ThoughtBubbleController thoughtBubble;
    private CharacterVisuals visuals;
    private List<Waypoint> patrolPoints;
    private int currentPointIndex;
    private float jokeTimer;
    private bool isWorking = true;

    private string[] jokesClients = {
        "Дорогие посетители! Напоминаем: очередь — это не наказание, а возможность для медитации.",
        "Почему бюрократы не женятся? Боятся, что придётся заполнять форму №ЛЮБОВЬ-1.",
        "Справка о том, что вы смеялись, будет готова через 3-5 рабочих дней.",
        "Тук-тук. Кто там? Инспектор. Инспектор чего? Инспектор вашего терпения!",
        "Говорят, смех продлевает жизнь. Но только при наличии формы №СМЕХ-7.",
        "Чем отличается клиент от кактуса? Кактус реже просит жалобную книгу.",
        "Если вы ждёте больше часа, вы имеете право на бесплатный стакан воды. В теории.",
        "Наш офис работает по принципу 'тише едешь — дольше ждёшь'.",
        "Почему в очереди все молчат? Потому что слова тоже нужно регистрировать.",
        "Объявление: потеряна очередь. Нашедшего просьба занять место в конце."
    };

    private string[] jokesStaff = {
        "Коллеги, помните: если бумага лежит ровно — вы просто плохо её кинули.",
        "Почему клерки не стареют? Потому что время на них не оформлено.",
        "Согласно приказу №404, счастье сотрудника не найдено. Повторите запрос позже.",
        "Что сказал один степлер другому? Держись, прорвёмся!",
        "Если вам кажется, что начальник вас не ценит — заполните форму №ОБИДА-2 в трёх экземплярах.",
        "Работа в офисе — это как бесконечный квест: собери 10 подписей, получи легендарную печать.",
        "Кто рано встаёт, тому весь день спать хочется. Особенно на совещаниях.",
        "Кофе — это топливо. Но топливо без формы №ЗАПРАВКА-1 считается контрабандой.",
        "Почему принтер всегда ломается в пятницу? Потому что у него тоже есть чувство юмора.",
        "Помните: любая ошибка — это не ваша вина, а недочёт в инструкции."
    };

    private string[] reactions = {
        "Ахахаха!", "Хм, спорно.", "И это шутка?", "Неплохо!", "Я не понял...",
        "Кхм.", "Забавно.", "Ужасно.", "Где тут смеяться?", "Браво!",
        "Это в мой огород?", "Грустно.", "Смеяться разрешено?", "Остроумно.", "Банально."
    };

    private void Start()
    {
        mover = GetComponent<AgentMover>();
        thoughtBubble = GetComponent<ThoughtBubbleController>();
        visuals = GetComponent<CharacterVisuals>();

        if (npcData != null)
        {
            var charVisuals = GetComponent<CharacterVisuals>();
            if (charVisuals != null) charVisuals.SetupFromTemporaryData(npcData);

            if (npcData.voiceProfile != null && thoughtBubble != null)
            {
                var staff = GetComponent<StaffController>();
                if (staff != null) staff.voiceProfile = npcData.voiceProfile;
            }
        }

        StartCoroutine(InitMusicAfterDelay());

        if (mode == ClownMode.Clients)
            patrolPoints = ScenePointsRegistry.Instance?.internPatrolPoints;
        else
            patrolPoints = GetStaffServicePoints();

        if (patrolPoints == null || patrolPoints.Count == 0)
        {
            patrolPoints = new List<Waypoint>(FindObjectsByType<Waypoint>(FindObjectsSortMode.None));
        }

        visuals?.SetEmotion(Emotion.Sad);
        StartCoroutine(LifecycleRoutine());
    }

    private System.Collections.IEnumerator InitMusicAfterDelay()
    {
        yield return null;
        if (MusicPlayer.Instance != null) MusicPlayer.Instance.PlayClownMusic();
    }

    private List<Waypoint> GetStaffServicePoints()
    {
        List<Waypoint> points = new List<Waypoint>();
        var allPoints = ScenePointsRegistry.Instance?.allServicePoints;
        if (allPoints != null)
        {
            foreach (var sp in allPoints)
            {
                if (sp != null && sp.clientStandPoint != null)
                    points.Add(sp.clientStandPoint);
            }
        }
        return points;
    }

    private IEnumerator LifecycleRoutine()
    {
        float workEndTime = Time.time + lifetime;
        
        // Заранее находим точку ухода
        Transform exitPoint = null;
        if (Managers.WaveManager.Instance != null && Managers.WaveManager.Instance.spawnPoint != null)
            exitPoint = Managers.WaveManager.Instance.spawnPoint;
        else if (Managers.ClientSpawner.Instance != null && Managers.ClientSpawner.Instance.exitWaypoint != null)
            exitPoint = Managers.ClientSpawner.Instance.exitWaypoint.transform;

        while (Time.time < workEndTime)
        {
            visuals?.SetEmotion(Emotion.Sad);

            if (patrolPoints.Count > 0)
            {
                Waypoint target = patrolPoints[currentPointIndex % patrolPoints.Count];
                mover.SetPath(PathfindingUtility.BuildPathTo(transform.position, target.transform.position, gameObject));
                
                yield return new WaitUntil(() => !mover.IsMoving() || Vector2.Distance(transform.position, target.transform.position) < 1.5f);
                
                visuals?.SetEmotion(Emotion.Thinking);
                yield return new WaitForSeconds(1f);

                if (Time.time > jokeTimer)
                {
                    visuals?.SetEmotion(Emotion.Happy);
                    string joke = GetRandomJoke();
                    thoughtBubble?.ShowPriorityMessage(joke, 4f, Color.yellow);
                    
                    TriggerReactions();
                    
                    jokeTimer = Time.time + jokeInterval;
                }

                currentPointIndex++;
                yield return new WaitForSeconds(2f);
            }
            else
            {
                yield return new WaitForSeconds(2f);
            }
        }

        // ========== БЛОК УХОДА ==========
        isWorking = false;
        mover.Stop();
        visuals?.SetEmotion(Emotion.Neutral);
        thoughtBubble?.ShowPriorityMessage("Моя смена окончена! До встречи!", 3f, Color.white);

        // Дождаться завершения возможного скольжения
        if (mover != null && mover.IsSlipping)
        {
            yield return new WaitForSeconds(2f);
        }
        // Если mover уничтожен - просто выходим
        if (mover == null)
        {
            Destroy(gameObject);
            yield break;
        }

        if (exitPoint != null)
        {
            mover.SetPath(PathfindingUtility.BuildPathTo(transform.position, exitPoint.position, gameObject));
            
            // Таймаут на движение к точке (15 секунд)
            float timeout = 15f;
            while (mover != null && mover.IsMoving() && Vector2.Distance(transform.position, exitPoint.position) > 1f && timeout > 0)
            {
                yield return new WaitForSeconds(0.5f);
                timeout -= 0.5f;
            }
            if (mover != null) mover.Stop();
        }
        else
        {
            // Если точка не найдена - просто уничтожаем через 2 секунды
            yield return new WaitForSeconds(2f);
        }

        Destroy(gameObject);
    }

    private void TriggerReactions()
    {
        float reactionChance = this.reactionChance;
        string[] reactionList = reactions;
        
        if (mode == ClownMode.Clients)
        {
            var clients = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None);
            
            foreach (var c in clients)
            {
                if (Random.value < reactionChance)
                {
                    string reaction = reactionList[Random.Range(0, reactionList.Length)];
                    
                    // Принудительная инициализация ThoughtBubbleController
                    var bubble = c.GetComponent<ThoughtBubbleController>();
                    if (bubble == null)
                    {
                        bubble = c.gameObject.AddComponent<ThoughtBubbleController>();
                    }
                    
                    if (bubble != null)
                    {
                        bubble.ShowPriorityMessage(reaction, 2.5f, Color.white);
                    }
                    else
                    {
                        c.ShowThoughtBubble(reaction, 2.5f);
                    }
                    c.RelieveStress(0.15f);
                }
            }
        }
        else
        {
            var staff = FindObjectsByType<StaffController>(FindObjectsSortMode.None);
            
            foreach (var s in staff)
            {
                if (Random.value < reactionChance)
                {
                    string reaction = reactionList[Random.Range(0, reactionList.Length)];
                    
                    // Проверка и инициализация thoughtBubble для staff
                    if (s.thoughtBubble == null)
                    {
                        s.thoughtBubble = s.GetComponent<ThoughtBubbleController>();
                    }
                    
                    s.thoughtBubble?.ShowPriorityMessage(reaction, 2.5f, Color.white);
                    s.ChangeStress(-15f);
                }
            }
        }
    }

    private string GetRandomJoke()
    {
        string[] jokes = (mode == ClownMode.Clients) ? jokesClients : jokesStaff;
        return jokes[Random.Range(0, jokes.Length)];
    }

    private string GetRandomReaction()
    {
        return reactions[Random.Range(0, reactions.Length)];
    }

    private void OnDestroy()
    {
        if (MusicPlayer.Instance != null) MusicPlayer.Instance.StopClownMusic();
    }
}
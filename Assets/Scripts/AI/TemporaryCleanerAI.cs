using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Utilities;
using Characters;
using Data;

public class TemporaryCleanerAI : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private float workDuration = 45f; // длительность работы в секундах
    [SerializeField] private float searchRadius = 50f; // увеличен радиус поиска

    [Header("NPC Data")]
    public Data.TemporaryNPCData npcData;

    [Header("Broom")]
    [SerializeField] private GameObject broomPrefab;
    [SerializeField] private float broomSwingAngle = 15f;
    [SerializeField] private float broomSwingSpeed = 10f;

    private AgentMover mover;
    private ThoughtBubbleController thoughtBubble;
    private CharacterVisuals visuals;
    private Transform broomTransform;
    private bool isWorking = true;

    private void Start()
    {
        mover = GetComponent<AgentMover>();
        thoughtBubble = GetComponent<ThoughtBubbleController>();
        visuals = GetComponent<CharacterVisuals>();
        if (mover == null) mover = gameObject.AddComponent<AgentMover>();

        if (npcData != null)
        {
            var charVisuals = GetComponent<CharacterVisuals>();
            if (charVisuals != null) charVisuals.SetupFromTemporaryData(npcData);
        }

        if (broomPrefab != null)
        {
            Transform hand = visuals?.GetAttachPoint(CharacterVisuals.AttachPointType.Hand);
            if (hand != null)
            {
                GameObject broom = Instantiate(broomPrefab, hand);
                broomTransform = broom.transform;
                broomTransform.localPosition = Vector3.zero;
                broomTransform.localRotation = Quaternion.identity;
            }
        }
        
        StartCoroutine(CleaningRoutine());
    }

    private IEnumerator SwingBroom()
    {
        if (broomTransform == null) yield break;
        Quaternion originalRot = broomTransform.localRotation;
        float timer = 0f;
        while (timer < 1.5f)
        {
            float angle = Mathf.Sin(timer * broomSwingSpeed) * broomSwingAngle;
            broomTransform.localRotation = originalRot * Quaternion.Euler(0, 0, angle);
            timer += Time.deltaTime;
            yield return null;
        }
        broomTransform.localRotation = originalRot;
    }

    private IEnumerator CleaningRoutine()
    {
        float endTime = Time.time + workDuration;
        
        // Заранее находим точку ухода
        Transform exitPoint = null;
        if (WaveManager.Instance != null && WaveManager.Instance.spawnPoint != null)
            exitPoint = WaveManager.Instance.spawnPoint;
        else if (ClientSpawner.Instance != null && ClientSpawner.Instance.exitWaypoint != null)
            exitPoint = ClientSpawner.Instance.exitWaypoint.transform;
        
        thoughtBubble?.ShowPriorityMessage("Начинаю уборку!", 2f, Color.white);
        visuals?.SetEmotion(Emotion.Thinking);

        while (Time.time < endTime)
        {
            if (!isWorking) yield break;
            
            MessPoint target = FindNearestMess();
            if (target == null || target.gameObject == null)
            {
                visuals?.SetEmotion(Emotion.Thinking);
                thoughtBubble?.ShowPriorityMessage("Всё чисто!", 1f, Color.green);
                yield return new WaitForSeconds(2f);
                continue;
            }
            
            visuals?.SetEmotion(Emotion.Working);
            Vector3 targetPos = target.transform.position;
            mover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPos, gameObject));
            yield return new WaitUntil(() => !mover.IsMoving() || Vector2.Distance(transform.position, targetPos) < 1.5f);
            
            if (!isWorking) yield break;
            
            // Проверка после достижения цели
            if (target == null || target.gameObject == null) continue;
            
            thoughtBubble?.ShowPriorityMessage("*вжик-вжик*", 1f, Color.cyan);
            yield return StartCoroutine(SwingBroom());
            
            // Повторная проверка перед уничтожением
            if (target != null && target.gameObject != null)
            {
                Destroy(target.gameObject);
            }
            
            // Пауза перед поиском следующего мусора
            yield return new WaitForSeconds(0.5f);
        }

        // Завершение работы
        isWorking = false;
        StopAllCoroutines();

        if (thoughtBubble != null)
            thoughtBubble.ShowPriorityMessage("Работа окончена! Ухожу.", 2.5f, Color.white);

        visuals?.SetEmotion(Emotion.Neutral);

        if (exitPoint != null)
        {
            if (mover != null)
            {
                mover.SetPath(PathfindingUtility.BuildPathTo(transform.position, exitPoint.position, gameObject));
                float timeout = 10f;
                float timer = 0f;
                while (mover.IsMoving() && Vector2.Distance(transform.position, exitPoint.position) > 1f && timer < timeout)
                {
                    yield return new WaitForSeconds(0.5f);
                    timer += 0.5f;
                }
                mover.Stop();
            }
        }

        Destroy(gameObject);
    }

    private MessPoint FindNearestMess()
    {
        var allMess = FindObjectsByType<MessPoint>(FindObjectsSortMode.None);
        Vector2 myPos = transform.position;
        
        // Фильтруем валидные MessPoint'ы
        var valid = allMess
            .Where(m => m != null && m.gameObject != null && Vector2.Distance(myPos, m.transform.position) <= searchRadius)
            .ToList();
        
        if (valid.Count == 0) return null;
        if (valid.Count == 1) return valid[0];
        
        // Сортируем по дистанции и берём случайный из первых трёх
        var sorted = valid
            .OrderBy(m => Vector2.Distance(myPos, m.transform.position))
            .Take(3)
            .ToList();
        
        return sorted[Random.Range(0, sorted.Count)];
    }
}
// Файл: DirectorAvatarController.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// --- ИЗМЕНЕНИЕ: Добавлены новые компоненты в RequireComponent ---
[RequireComponent(typeof(AgentMover))]
[RequireComponent(typeof(CharacterVisuals))]
[RequireComponent(typeof(ThoughtBubbleController))]
public class DirectorAvatarController : MonoBehaviour
{
    public static DirectorAvatarController Instance { get; private set; }

    // --- ИЗМЕНЕНИЕ: Обновлённый enum с новыми состояниями ---
    public enum DirectorState { Idle, MovingToPoint, AtDesk, CarryingDocuments, Supervising, Talking, Scared, Happy, Enraged, GoingForDocuments }

    [Header("Ссылки")]
    private AgentMover agentMover;
    // --- НОВЫЕ ПОЛЯ: Ссылки на компоненты для эмоций и мыслей ---
    private CharacterVisuals visuals;
    private ThoughtBubbleController thoughtBubble;

    [Header("Настройки Кабинета")]
    [Tooltip("Точка в кабинете (кресло), на которой Директор должен находиться для доступа к UI")]
    public Transform directorChairPoint;
    private DirectorState currentState = DirectorState.Idle;
    private Waypoint[] allWaypoints;
    private StackHolder stackHolder;

    public bool IsAtDesk { get; private set; } = false;

    void Awake()
    {
        Instance = this;
        agentMover = GetComponent<AgentMover>();
        stackHolder = GetComponent<StackHolder>();
        allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        // --- НОВОЕ: Получаем ссылки при старте ---
        visuals = GetComponent<CharacterVisuals>();
        thoughtBubble = GetComponent<ThoughtBubbleController>();
    }

    void Update()
    {
        if (directorChairPoint != null && Vector2.Distance(transform.position, directorChairPoint.position) < 0.5f)
        {
            if (!IsAtDesk)
            {
                // --- ИЗМЕНЕНИЕ: Используем новое состояние "На рабочем месте" ---
                SetState(DirectorState.AtDesk);
                agentMover.Stop();
            }
            IsAtDesk = true;
        }
        else
        {
            if (IsAtDesk) // Если только что покинул стол
            {
                // Переходим в состояние по умолчанию, если нет другой задачи
                if (currentState == DirectorState.AtDesk) SetState(DirectorState.Idle);
            }
            IsAtDesk = false;
        }
    }

	public void ForceSetAtDeskState(bool atDesk)
		{
			IsAtDesk = atDesk;
			if (atDesk)
			{
				SetState(DirectorState.AtDesk); // Используем ваш существующий метод для смены состояния
			}
		}

public void TeleportTo(Vector3 position)
{
    if (agentMover != null && agentMover.agent != null)
    {
        // Выключаем NavMeshAgent, чтобы он не сопротивлялся
        agentMover.agent.enabled = false;
    }

    // Мгновенно перемещаем объект
    transform.position = position;

    if (agentMover != null && agentMover.agent != null)
    {
        // Включаем NavMeshAgent обратно. Теперь он будет работать с новой позиции.
        agentMover.agent.enabled = true;
    }
    Debug.Log($"[DirectorAvatarController] Директор телепортирован в {position}");
}

    public void MoveToWaypoint(Waypoint targetWaypoint)
    {
        if (currentState == DirectorState.CarryingDocuments)
        {
            Debug.Log("Директор: Не могу идти, несу документы!");
            return;
        }

        Debug.Log($"--- ДИРЕКТОР: Получена команда идти к '{targetWaypoint.name}' ---");
        StopAllCoroutines();
        StartCoroutine(MoveToTargetRoutine(targetWaypoint.transform.position, DirectorState.Idle));
    }

    public void CollectDocuments(DocumentStack targetStack)
    {
        if (currentState != DirectorState.Idle && currentState != DirectorState.AtDesk)
        {
            Debug.Log("Директор: Не могу забрать документы, я занят!");
            return;
        }
        if (targetStack.IsEmpty)
        {
            Debug.Log("Директор: Кликнули на пустую стопку, игнорирую.");
            return;
        }

        Debug.Log($"--- ДИРЕКТОР: Получена команда забрать документы со стопки '{targetStack.name}' ---");
        StopAllCoroutines();
        StartCoroutine(CollectAndDeliverRoutine(targetStack));
    }
	
	public void GoToDesk()
{
    // Проверяем, что мы уже не в процессе переноски документов
    if (currentState == DirectorState.CarryingDocuments || currentState == DirectorState.GoingForDocuments)
    {
        Debug.Log("Директор: Не могу пойти к столу, занят документами!");
        return;
    }

    // Находим ближайший вейпоинт к креслу и идем туда
    if (directorChairPoint != null)
    {
        Waypoint targetWaypoint = FindNearestVisibleWaypoint(directorChairPoint.position);
        if (targetWaypoint != null)
        {
            MoveToWaypoint(targetWaypoint);
        }
    }
}

    private IEnumerator CollectAndDeliverRoutine(DocumentStack stack)
    {
        SetState(DirectorState.GoingForDocuments);
        yield return StartCoroutine(MoveToTargetRoutine(stack.transform.position, DirectorState.GoingForDocuments));

        Debug.Log("Директор: Забираю документы со стопки.");
        int docCount = stack.TakeEntireStack();
        if (docCount > 0)
        {
            stackHolder?.ShowStack(docCount, stack.maxStackSize);
            SetState(DirectorState.CarryingDocuments);
            Transform archivePoint = ArchiveManager.Instance.RequestDropOffPoint();
            if (archivePoint != null)
            {
                Debug.Log("Директор: Несу документы в архив.");
                yield return StartCoroutine(MoveToTargetRoutine(archivePoint.position, DirectorState.CarryingDocuments));

                Debug.Log("Директор: Складываю документы в архив.");
                for (int i = 0; i < docCount; i++)
                {
                    ArchiveManager.Instance.mainDocumentStack.AddDocumentToStack();
                }
                stackHolder?.HideStack();
                ArchiveManager.Instance.FreeOverflowPoint(archivePoint);
            }
        }

        SetState(DirectorState.Idle);
        Debug.Log("--- ДИРЕКТОР: Задача выполнена, перехожу в режим ожидания. ---");
    }

    private IEnumerator MoveToTargetRoutine(Vector2 targetPosition, DirectorState stateAfterArrival)
    {
        SetState(DirectorState.MovingToPoint);
        Debug.Log($"Директор: Строю маршрут к точке {targetPosition}...");

        Queue<Waypoint> path = BuildPathTo(targetPosition);
        if (path != null && path.Count > 0)
        {
            Debug.Log($"Директор: <color=green>Маршрут построен успешно!</color> Начинаю движение.");
            agentMover.SetPath(path);
            yield return new WaitUntil(() => !agentMover.IsMoving());
            Debug.Log("Директор: <color=green>Прибыл на место.</color>");
        }
        else
        {
            Debug.LogError("Директор: <color=red>Не удалось построить маршрут!</color> Остаюсь на месте.");
        }

        SetState(stateAfterArrival);
    }

    private void SetState(DirectorState newState)
    {
        if (currentState == newState) return;
        Debug.Log($"Директор: Меняю состояние с '{currentState}' на '{newState}'.");
        currentState = newState;
        // --- ИЗМЕНЕНИЕ: Обновляем эмоцию при смене состояния ---
        visuals?.SetEmotionForState(newState);
    }

    // --- НОВЫЙ МЕТОД: Чтобы другие скрипты могли получать текущее состояние ---
    public DirectorState GetCurrentState()
    {
        return currentState;
    }

    private Queue<Waypoint> BuildPathTo(Vector2 targetPos)
    {
        var path = new Queue<Waypoint>();
        if (allWaypoints == null || allWaypoints.Length == 0)
        {
            Debug.LogError("Ошибка поиска пути: Вейпоинты не найдены на сцене!");
            return path;
        }

        Waypoint startNode = FindNearestVisibleWaypoint(transform.position);
        Waypoint endNode = FindNearestVisibleWaypoint(targetPos);
        if (startNode == null || endNode == null)
        {
            Debug.LogError($"Ошибка поиска пути: Не удалось найти видимые точки. Старт: {(startNode != null ? startNode.name : "NULL")}, Финиш: {(endNode != null ? endNode.name : "NULL")}");
            return path;
        }

        Debug.Log($"Поиск пути: Старт - '{startNode.name}', Финиш - '{endNode.name}'.");
        Dictionary<Waypoint, float> distances = new Dictionary<Waypoint, float>();
        Dictionary<Waypoint, Waypoint> previous = new Dictionary<Waypoint, Waypoint>();
        var queue = new PriorityQueue<Waypoint>();
        foreach (var wp in allWaypoints)
        {
            distances[wp] = float.MaxValue;
            previous[wp] = null;
        }

        distances[startNode] = 0;
        queue.Enqueue(startNode, 0);

        while (queue.Count > 0)
        {
            Waypoint current = queue.Dequeue();
            if (current == endNode)
            {
                ReconstructPath(previous, endNode, path);
                return path;
            }

            foreach (var neighbor in current.neighbors)
            {
                if (neighbor == null) continue;
                // --- ИЗМЕНЕНИЕ: Логика пути была исправлена в предыдущем шаге ---
                // (Убрано ограничение на StaffOnly)

                float newDist = distances[current] + Vector2.Distance(current.transform.position, neighbor.transform.position);
                if (distances.ContainsKey(neighbor) && newDist < distances[neighbor])
                {
                    distances[neighbor] = newDist;
                    previous[neighbor] = current;
                    queue.Enqueue(neighbor, newDist);
                }
            }
        }
        return path;
    }

    private void ReconstructPath(Dictionary<Waypoint, Waypoint> previous, Waypoint goal, Queue<Waypoint> path)
    {
        List<Waypoint> pathList = new List<Waypoint>();
        for (Waypoint at = goal; at != null; at = previous[at])
        {
            pathList.Add(at);
        }
        pathList.Reverse();
        path.Clear();
        foreach (var wp in pathList)
        {
            path.Enqueue(wp);
        }
    }

    private Waypoint FindNearestVisibleWaypoint(Vector2 position)
    {
        if (allWaypoints == null) return null;
        Waypoint bestWaypoint = null;
        float minDistance = float.MaxValue;

        foreach (var wp in allWaypoints)
        {
            if (wp == null) continue;
            float distance = Vector2.Distance(position, wp.transform.position);
            if (distance < minDistance)
            {
                RaycastHit2D hit = Physics2D.Linecast(position, wp.transform.position, LayerMask.GetMask("Obstacles"));
                if (hit.collider == null)
                {
                    minDistance = distance;
                    bestWaypoint = wp;
                }
            }
        }
        return bestWaypoint;
    }

    private class PriorityQueue<T>
    {
        private List<KeyValuePair<T, float>> elements = new List<KeyValuePair<T, float>>();
        public int Count => elements.Count;
        public void Enqueue(T item, float priority) { elements.Add(new KeyValuePair<T, float>(item, priority)); }
        public T Dequeue()
        {
            int bestIndex = 0;
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i].Value < elements[bestIndex].Value) { bestIndex = i; }
            }
            T bestItem = elements[bestIndex].Key;
            elements.RemoveAt(bestIndex);
            return bestItem;
        }
    }
}
// Файл: Assets/Scripts/Managers/ArchiveRequestManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Required for Linq methods like FirstOrDefault

// Простой класс для хранения информации о запросе
public class ArchiveRequest
{
    public ClerkController RequestingRegistrar; // Регистратор, создавший запрос
    public ClientPathfinding WaitingClient; // Клиент, ожидающий документ
    public bool IsFulfilled = false; // Отметка о выполнении запроса архивариусом
    public bool IsTaken = false; // Отметка о том, что архивариус взял запрос в работу (чтобы другой не взял)
}

public class ArchiveRequestManager : MonoBehaviour
{
    // --- Singleton Pattern ---
    public static ArchiveRequestManager Instance { get; private set; }

    // --- Используем List вместо Queue для возможности поиска ---
    private List<ArchiveRequest> pendingRequestsList = new List<ArchiveRequest>();

    void Awake()
    {
        // Стандартная реализация Singleton
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[ArchiveRequestManager] Уничтожен дубликат на {gameObject.name}");
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // Раскомментируй, если менеджер должен быть "бессмертным"
            Debug.Log("[ArchiveRequestManager] Instance установлен.");
        }
    }
    // --- End Singleton ---

    /// <summary>
    /// Создает новый запрос в архив и добавляет его в список ожидания.
    /// </summary>
    /// <param name="registrar">Регистратор, создающий запрос.</param>
    /// <param name="client">Клиент, для которого нужен документ.</param>
    public void CreateRequest(ClerkController registrar, ClientPathfinding client)
    {
        // Проверки входных данных
        if (registrar == null || client == null)
        {
            Debug.LogError("[ArchiveRequestManager] Попытка создать запрос с null регистратором или клиентом!");
            return;
        }

        // Проверяем, нет ли уже активного запроса для этого клиента
        if (pendingRequestsList.Any(req => req.WaitingClient == client && !req.IsFulfilled))
        {
            Debug.LogWarning($"[ArchiveRequestManager] Для клиента {client.name} уже существует активный запрос.");
            return; // Не создаем дублирующий запрос
        }


        var newRequest = new ArchiveRequest
        {
            RequestingRegistrar = registrar,
            WaitingClient = client,
            IsFulfilled = false,
            IsTaken = false // Изначально запрос не взят
        };

        pendingRequestsList.Add(newRequest); // Добавляем в конец списка
        Debug.Log($"[ArchiveRequestManager] Регистратор {registrar.name} создал запрос для клиента {client.name}. В очереди: {pendingRequestsList.Count}");
    }

    /// <summary>
    /// Возвращает следующий НЕВЗЯТЫЙ запрос из списка для обработки архивариусом.
    /// Помечает запрос как взятый.
    /// </summary>
    /// <returns>Следующий доступный запрос или null, если таких нет.</returns>
    public ArchiveRequest GetNextRequest()
    {
        // Ищем первый запрос, который еще не был взят (IsTaken == false)
        ArchiveRequest request = pendingRequestsList.FirstOrDefault(req => !req.IsTaken && !req.IsFulfilled);

        if (request != null)
        {
            request.IsTaken = true; // Помечаем, что запрос взят в работу
            Debug.Log($"[ArchiveRequestManager] Архивариус взял запрос для {request.WaitingClient?.name}. Запрос помечен как IsTaken.");
            // Не удаляем из списка сразу, удалим после выполнения или отмены
        }
        else
        {
             // Debug.Log("[ArchiveRequestManager] Нет новых запросов для архивариуса."); // Можно раскомментировать для отладки
        }
        return request;
    }

    /// <summary>
    /// Находит ПЕРВЫЙ активный (невыполненный) запрос, созданный указанным регистратором.
    /// Используется регистратором для проверки статуса своего запроса. НЕ помечает запрос как взятый.
    /// </summary>
    /// <param name="registrar">Регистратор, чей запрос ищем.</param>
    /// <returns>Найденный запрос или null.</returns>
    public ArchiveRequest GetOurRequest(ClerkController registrar)
    {
        if (registrar == null) return null;

        // Ищем первый НЕ выполненный запрос от этого регистратора
        return pendingRequestsList.FirstOrDefault(req => req.RequestingRegistrar == registrar && !req.IsFulfilled);
    }

    /// <summary>
    /// Проверяет, есть ли в списке ожидающие (не взятые и не выполненные) запросы.
    /// </summary>
    /// <returns>True, если есть ожидающие запросы.</returns>
    public bool HasPendingRequests()
    {
        // Проверяем, есть ли хотя бы один запрос, который не выполнен И не взят
        return pendingRequestsList.Any(req => !req.IsFulfilled && !req.IsTaken);
    }

    /// <summary>
    /// Завершает запрос (успешно выполнен). Вызывается архивариусом.
    /// </summary>
    /// <param name="request">Выполненный запрос.</param>
     public void FulfillRequest(ArchiveRequest request) {
         if (request != null) {
             request.IsFulfilled = true;
              Debug.Log($"[ArchiveRequestManager] Запрос для {request.WaitingClient?.name} помечен как выполненный (IsFulfilled = true).");
             // Можно удалить из списка здесь, если выполненные запросы больше не нужны
             // pendingRequestsList.Remove(request);
         }
     }


    /// <summary>
    /// Отменяет запрос (например, если клиент ушел). Удаляет запрос из списка.
    /// </summary>
    /// <param name="request">Запрос для отмены.</param>
    public void CancelRequest(ArchiveRequest request)
    {
        if (request != null && pendingRequestsList.Contains(request))
        {
            bool removed = pendingRequestsList.Remove(request);
            if (removed)
            {
                Debug.LogWarning($"[ArchiveRequestManager] Запрос для {request.WaitingClient?.name} (Регистратор: {request.RequestingRegistrar?.name}) ОТМЕНЕН и удален из списка.");
            }
        }
         // Дополнительно чистим список от "мертвых" запросов (например, если клиент был уничтожен)
         pendingRequestsList.RemoveAll(req => req.WaitingClient == null || req.RequestingRegistrar == null);
    }

     /// <summary>
     /// Очищает все запросы (например, при начале нового дня или загрузке).
     /// </summary>
     public void ClearAllRequests()
     {
         pendingRequestsList.Clear();
         Debug.Log("[ArchiveRequestManager] Все запросы в архив очищены.");
     }

} // Конец класса ArchiveRequestManager
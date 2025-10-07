using UnityEngine;
using System.Collections.Generic;

// Простой класс для хранения информации о запросе
public class ArchiveRequest
{
    public ClerkController RequestingRegistrar;
    public ClientPathfinding WaitingClient;
    public bool IsFulfilled = false;
}

public class ArchiveRequestManager : MonoBehaviour
{
    public static ArchiveRequestManager Instance { get; private set; }

    private Queue<ArchiveRequest> pendingRequests = new Queue<ArchiveRequest>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); } else { Instance = this; }
    }

    public void CreateRequest(ClerkController registrar, ClientPathfinding client)
    {
        var newRequest = new ArchiveRequest { RequestingRegistrar = registrar, WaitingClient = client };
        pendingRequests.Enqueue(newRequest);
        Debug.Log($"[ArchiveRequest] Регистратор {registrar.name} создал запрос для клиента {client.name}.");
    }

    public ArchiveRequest GetNextRequest()
    {
        if (pendingRequests.Count > 0)
        {
            return pendingRequests.Dequeue();
        }
        return null;
    }

    public bool HasPendingRequests()
    {
        return pendingRequests.Count > 0;
    }
}
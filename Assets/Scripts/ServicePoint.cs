// Файл: ServicePoint.cs
using UnityEngine;
using Characters;

public class ServicePoint : MonoBehaviour
{
    [Tooltip("ID этой стойки (1 для Стойки 1, 2 для Стойки 2 и т.д.)")]
    public int deskId;
	[Tooltip("Понятное имя для UI (например, 'Касса' или 'Окно Регистрации')")]
    public string friendlyName;
    [Tooltip("Точка, где должен стоять клерк")]
    public Transform clerkStandPoint;
    [Tooltip("Точка, где должен стоять клиент (это и есть insideWaypoint для зоны)")]
    public Waypoint clientStandPoint;
    [Tooltip("Точка на столе, куда кладется документ")]
    public Transform documentPointOnDesk;
    
    [Tooltip("Лоток для денег на кассе")]
    public Transform moneyTrayPoint;
    [Tooltip("Ссылка на стопку документов, связанную с этим местом")]
    public DocumentStack documentStack;
    
    // --- НОВОЕ ПОЛЕ ---
    [Tooltip("Точка, к которой будет подходить стажер, чтобы забрать документы со стола.")]
    public Transform internCollectionPoint;

    private StaffController assignedStaff;

    // === НОВАЯ СИСТЕМА СИНХРОНИЗАЦИИ ===
    [Tooltip("Текущий клиент, направленный к этой стойке")]
    public ClientPathfinding CurrentClient { get; private set; }
    
    [Tooltip("Клиент физически подошел к стойке")]
    public bool IsClientPhysicallyReady { get; private set; }

    public void AssignClient(ClientPathfinding client)
    {
        CurrentClient = client;
        IsClientPhysicallyReady = false;
        Debug.Log($"[ServicePoint {name}] Ожидаю клиента {client.name}");
    }

    public void SetClientReady()
    {
        if (CurrentClient != null)
        {
            IsClientPhysicallyReady = true;
            Debug.Log($"[ServicePoint {name}] Клиент {CurrentClient.name} подошел к стойке!");
        }
    }

    public void ClearClient()
    {
        CurrentClient = null;
        IsClientPhysicallyReady = false;
    }
    // ===================================

    public void ClearAssignedStaff()
    {
        assignedStaff = null;
    }

    public void SetAssignedStaff(StaffController staff)
    {
        assignedStaff = staff;
    }

    public StaffController GetAssignedStaff()
    {
        return assignedStaff;
    }
}
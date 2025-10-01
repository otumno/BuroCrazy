// Файл: Assets/Scripts/Managers/GuardManager.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GuardManager : MonoBehaviour
{
    public static GuardManager Instance { get; private set; }

    [Header("Настройки менеджера")]
    [Tooltip("Шанс (0-1), с которым охранник заметит попытку кражи")]
    public float chanceToNoticeTheft = 0.6f;
    [Tooltip("Перетащите сюда объект барьера со сцены")]
    public SecurityBarrier securityBarrier;

    // --- ИЗМЕНЕНО: Списки теперь являются "доской объявлений" ---
    private List<ClientPathfinding> reportedThieves = new List<ClientPathfinding>();
    private List<ClientPathfinding> reportedViolators = new List<ClientPathfinding>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; }
    }

    void Start()
{
    // При загрузке сцены находим барьер через реестр
    if (securityBarrier == null)
    {
        if(ScenePointsRegistry.Instance != null && ScenePointsRegistry.Instance.securityBarrier != null)
        {
             securityBarrier = ScenePointsRegistry.Instance.securityBarrier;
        }
        else
        {
            // Fallback if not set in registry
            securityBarrier = FindFirstObjectByType<SecurityBarrier>();
        }
    }
}
    
    // --- НОВЫЙ МЕТОД: "Приколоть" заявку о краже на доску ---
    public void ReportTheft(ClientPathfinding thief)
    {
        if (thief != null && !reportedThieves.Contains(thief) && !reportedViolators.Contains(thief))
        {
            Debug.Log($"[GuardManager] ПОЛУЧЕН СИГНАЛ: Кража от {thief.name}!");
            reportedThieves.Add(thief);
        }
    }

    // --- НОВЫЙ МЕТОД: "Приколоть" заявку о нарушителе на доску ---
    public void ReportViolator(ClientPathfinding violator)
    {
        if (violator != null && !reportedViolators.Contains(violator) && !reportedThieves.Contains(violator))
        {
            Debug.Log($"[GuardManager] ПОЛУЧЕН СИГНАЛ: Буйство от {violator.name}!");
            reportedViolators.Add(violator);
        }
    }

    // --- НОВЫЙ МЕТОД: Охранник "смотрит" на доску в поисках воров ---
    public ClientPathfinding GetThiefToCatch()
    {
        reportedThieves.RemoveAll(t => t == null); // Чистим список от "мертвых душ"
        return reportedThieves.FirstOrDefault();
    }
    
    // --- НОВЫЙ МЕТОД: Охранник "смотрит" на доску в поисках нарушителей ---
    public ClientPathfinding GetViolatorToHandle()
    {
        reportedViolators.RemoveAll(v => v == null); // Чистим список
        return reportedViolators.FirstOrDefault();
    }

    // --- НОВЫЙ МЕТОД: Охранник "снимает" заявку с доски, когда берет ее в работу ---
    public void MarkTaskAsTaken(ClientPathfinding target)
    {
        if (reportedThieves.Contains(target))
        {
            reportedThieves.Remove(target);
        }
        if (reportedViolators.Contains(target))
        {
            reportedViolators.Remove(target);
        }
    }

    // --- УДАЛЕНО: Все старые методы, такие как AssignToChase, InterruptWithNewTask, IsAvailableAndOnDuty, 
    // так как они больше не соответствуют новой логике. ---
}
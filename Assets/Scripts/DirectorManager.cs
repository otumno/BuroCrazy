// Файл: DirectorManager.cs
using UnityEngine;
using System.Collections.Generic;

public class DirectorManager : MonoBehaviour
{
    public static DirectorManager Instance { get; private set; }

    [Header("Настройки")]
    [Tooltip("Список всех доступных приказов директора")]
    public List<DirectorOrder> allOrders;
    
    [Header("Текущее состояние")]
    public DailyMandates currentMandates;
    public int currentStrikes = 0;
    public List<DirectorOrder> activeOrders = new List<DirectorOrder>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    public void AddStrike()
    {
        currentStrikes++;
        Debug.Log($"Директор получил страйк! Текущее количество: {currentStrikes}");
        if (currentStrikes >= 3)
        {
            Debug.Log("GAME OVER: Внеплановая проверка!");
            // TODO: Здесь будет логика Game Over
        }
    }
    
    public void CheckDailyMandates()
    {
        if (currentMandates == null)
        {
            Debug.LogWarning("Нормы дня не установлены!");
            return;
        }

        bool failedMandate = false;

        // 1. Проверка загруженности архива
        if (ArchiveManager.Instance != null && ArchiveManager.Instance.GetCurrentDocumentCount() > currentMandates.maxArchiveDocumentCount)
        {
            Debug.Log($"Норма дня не выполнена: слишком много документов в архиве ({ArchiveManager.Instance.GetCurrentDocumentCount()}/{currentMandates.maxArchiveDocumentCount})");
            AddStrike();
            failedMandate = true;
        }

        // 2. Проверка загруженности столов
        DocumentStack[] allStacks = FindObjectsByType<DocumentStack>(FindObjectsSortMode.None);
        foreach (var stack in allStacks)
        {
            // Игнорируем архивную стопку
            if (ArchiveManager.Instance != null && stack == ArchiveManager.Instance.mainDocumentStack) continue;

            if (stack.CurrentSize > currentMandates.maxDeskDocumentCount)
            {
                Debug.Log($"Норма дня не выполнена: слишком много документов на столе {stack.name} ({stack.CurrentSize}/{currentMandates.maxDeskDocumentCount})");
                AddStrike();
                failedMandate = true;
                break; // Достаточно одного проваленного стола
            }
        }
        
        // 3. Проверка обслуженных клиентов
        if (ClientPathfinding.clientsExitedProcessed < currentMandates.minProcessedClients)
        {
            Debug.Log($"Норма дня не выполнена: обслужено слишком мало клиентов ({ClientPathfinding.clientsExitedProcessed}/{currentMandates.minProcessedClients})");
            AddStrike();
            failedMandate = true;
        }
        
        // 4. Проверка недовольных клиентов
        if (ClientPathfinding.clientsExitedAngry > currentMandates.maxUpsetClients)
        {
            Debug.Log($"Норма дня не выполнена: слишком много недовольных клиентов ({ClientPathfinding.clientsExitedAngry}/{currentMandates.maxUpsetClients})");
            AddStrike();
            failedMandate = true;
        }

        if (!failedMandate)
        {
            Debug.Log("Все нормы дня выполнены! Отличная работа, Директор!");
        }
    }

    public void ApplyOrderEffects(DirectorOrder order)
    {
        // TODO: Здесь будет логика применения эффектов приказов
        // Например:
        // ClientSpawner.Instance.SetSpawnRate(order.newSpawnRate);
    }
}
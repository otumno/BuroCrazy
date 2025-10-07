// Файл: Assets/Scripts/Characters/Controllers/ServiceWorkerController.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(AgentMover), typeof(CharacterStateLogger))]
public class ServiceWorkerController : StaffController
{
    public enum WorkerState { Idle, SearchingForWork, GoingToMess, Cleaning, GoingToBreak, OnBreak, GoingToToilet, AtToilet, OffDuty, StressedOut, Patrolling, DeliveringValuables } // <-- Добавьте DeliveringValuables
    
    [Header("Состояние Уборщика")]
    private WorkerState currentState = WorkerState.OffDuty;

    [Header("Уникальные параметры Уборщика")]
    public float cleaningTimeTrash;
    public float cleaningTimePuddle;
    public float cleaningTimePerDirtLevel;
    public float maxStress;
    public float stressGainPerMess;
    public float stressReliefRate;

    [Header("Объекты (Prefab)")]
    public GameObject nightLight;
    public Transform broomTransform;
	private GameObject instantiatedTrashBag;
	public GameObject TrashBagObject => instantiatedTrashBag;
    private Quaternion initialBroomRotation;
	
	public float minIdleWait;
	public float maxIdleWait;

    // --- ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ ИСПОЛЬЗОВАНИЯ ИСПОЛНИТЕЛЯМИ ---

    public void SetState(WorkerState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        if(logger != null) 
        {
            logger.LogState(GetStatusInfo());
        }
        if(visuals != null)
        {
            visuals.SetEmotionForState(newState);
        }
    }

protected override ActionExecutor GetIdleActionExecutor()
    {
        // Идти в свою "подсобку"
        return gameObject.AddComponent<GoToJanitorHomeExecutor>();
    }

    public override string GetCurrentStateName()
    {
        return currentState.ToString();
    }

protected override ActionExecutor GetBurnoutActionExecutor()
{
    return gameObject.AddComponent<ScreamInClosetExecutor>();
}

public WorkerState GetCurrentState()
{
    return currentState;
}

    public IEnumerator MoveToTarget(Vector2 targetPosition, WorkerState stateOnArrival)
    {
        agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, this.gameObject));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        SetState(stateOnArrival);
    }
    
    // --- РЕАЛИЗАЦИЯ БАЗОВЫХ МЕТОДОВ ---

    public override bool IsOnBreak()
    {
        return currentState == WorkerState.OnBreak ||
               currentState == WorkerState.GoingToBreak ||
               currentState == WorkerState.AtToilet ||
               currentState == WorkerState.GoingToToilet ||
               currentState == WorkerState.StressedOut;
    }

    public override string GetStatusInfo()
    {
        return currentState.ToString();
    }

    // --- УНИКАЛЬНЫЙ МЕТОД ИНИЦИАЛИЗАЦИИ ДЛЯ УБОРЩИКА ---
    public void InitializeFromData(RoleData data)
    {
        if (agentMover != null)
        {
            agentMover.moveSpeed = data.moveSpeed;
            agentMover.priority = data.priority;
        }
        
        this.spriteCollection = data.spriteCollection;
        this.stateEmotionMap = data.stateEmotionMap;
        if(visuals != null)
        {
            visuals.EquipAccessory(data.accessoryPrefab);
        }

        this.cleaningTimeTrash = data.worker_cleaningTimeTrash;
        this.cleaningTimePuddle = data.worker_cleaningTimePuddle;
        this.cleaningTimePerDirtLevel = data.worker_cleaningTimePerDirtLevel;
        this.maxStress = data.worker_maxStress;
        this.stressGainPerMess = data.worker_stressGainPerMess;
        this.stressReliefRate = data.worker_stressReliefRate;
		this.minIdleWait = data.minIdleWait;
		this.maxIdleWait = data.maxIdleWait;
		
		if (data.worker_trashBagPrefab != null)
    {
        // Убеждаемся, что у нас есть точка крепления в руках (handAttachPoint)
        var visuals = GetComponent<CharacterVisuals>();
        if (visuals != null && visuals.handAttachPoint != null)
        {
            // Уничтожаем старый мешок, если он вдруг остался от предыдущей роли
            if (instantiatedTrashBag != null) { Destroy(instantiatedTrashBag); }

            instantiatedTrashBag = Instantiate(data.worker_trashBagPrefab, visuals.handAttachPoint);
            instantiatedTrashBag.SetActive(false);
        }
    }
    }

    // --- МЕТОДЫ UNITY ---

    protected override void Awake()
{
    base.Awake(); // Вызываем Awake базового класса
    
    var references = GetComponent<StaffPrefabReferences>();
    if (references != null)
    {
        this.nightLight = references.nightLight;
    }
    
    if (broomTransform != null)
    {
        initialBroomRotation = broomTransform.localRotation;
    }
	
}
}
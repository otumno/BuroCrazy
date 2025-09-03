// Файл: StaffController.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Абстрактный класс - его нельзя повесить на объект напрямую, только унаследоваться от него.
public abstract class StaffController : MonoBehaviour
{
    [Header("График работы")]
    [Tooltip("Настройте рабочие периоды ниже с помощью галочек.")]
    public List<string> workPeriods;

    [Header("Стандартные точки")]
    public Transform homePoint;
    public Transform kitchenPoint;
    public Transform staffToiletPoint;

    [Header("Звуки смены")]
    public AudioClip startShiftSound;
    public AudioClip endShiftSound;

    protected bool isOnDuty = false;
    protected Coroutine currentAction;
    protected AgentMover agentMover;
    protected CharacterStateLogger logger;

    // Публичный метод, чтобы ClientSpawner мог узнать статус
    public bool IsOnDuty() => isOnDuty;

    // Методы, которые будут вызываться из ClientSpawner
    public abstract void StartShift();
    public abstract void EndShift();

    // Общая логика для всех наследников
    protected virtual void Awake()
    {
        agentMover = GetComponent<AgentMover>();
        logger = GetComponent<CharacterStateLogger>();
    }

    protected virtual void Start()
    {
        // При старте игры все сотрудники по умолчанию вне смены и дома
        if (homePoint != null)
        {
            transform.position = homePoint.position;
        }
    }
}
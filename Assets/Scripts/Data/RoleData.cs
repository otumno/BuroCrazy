// Файл: Scripts/Data/RoleData.cs
using UnityEngine;

[CreateAssetMenu(fileName = "RoleData_New", menuName = "Bureau/Role Data")]
public class RoleData : ScriptableObject
{
    [Header("Идентификация и Базовые Параметры")]
    public StaffController.Role roleType;
    public float moveSpeed = 3f;
    public int priority = 1;
    
    [Header("Внешний вид")]
    public EmotionSpriteCollection spriteCollection;
    public StateEmotionMap stateEmotionMap;
    public GameObject accessoryPrefab;
    
    [Header("Анимация ходьбы")]
    public Sprite idleSprite;
    public Sprite walkSprite1;
    public Sprite walkSprite2;

    // --- НАШИ НОВЫЕ РАЗДЕЛЫ ---

    [Header("Специфика Охранника (Guard)")]
    public float guard_minWaitTime = 1f;
    public float guard_maxWaitTime = 3f;
    public float guard_chaseSpeedMultiplier = 1.5f;
    public float guard_talkTime = 3f;
    public float guard_timeInToilet = 15f;
    public float guard_maxStress = 100f;
    public float guard_stressGainPerViolator = 25f;
    public float guard_stressReliefRate = 10f;

    [Header("Специфика Клерка (Clerk)")]
    public float clerk_timeInToilet = 10f;
    public float clerk_clientArrivalTimeout = 16f;
    public float clerk_maxStress = 100f;
    public float clerk_stressGainPerClient = 5f;
    public float clerk_stressReliefRate = 10f;
    
    [Header("Специфика Уборщика (Service Worker)")]
    public float worker_cleaningTimeTrash = 2f;
    public float worker_cleaningTimePuddle = 4f;
    public float worker_cleaningTimePerDirtLevel = 1.5f;
	public GameObject worker_trashBagPrefab;
    public float worker_maxStress = 100f;
    public float worker_stressGainPerMess = 2f;
    public float worker_stressReliefRate = 10f;
	
	[Header("Специфика Кассира (Cashier)")]
	[Tooltip("Множитель шанса на кражу. 1.0 = стандартный шанс (на основе навыка).")]
	public float cashier_corruptionChanceMultiplier = 1.0f;
	[Tooltip("Максимальный процент от суммы, который кассир может украсть за раз (0.0 до 1.0).")]
	[Range(0f, 1f)]
	public float cashier_maxSkimAmount = 0.3f; // 30% по умолчанию
	
	[Header("Параметры бездействия (Idle)")]
    public float minIdleWait = 5f;
    public float maxIdleWait = 10f;
}
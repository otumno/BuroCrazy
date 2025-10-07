// Файл: Assets/Scripts/Characters/Controllers/ClerkController.cs - ПОЛНАЯ ОБНОВЛЕННАЯ ВЕРСИЯ
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AgentMover))]
[RequireComponent(typeof(CharacterStateLogger))]
[RequireComponent(typeof(StackHolder))]
public class ClerkController : StaffController, IServiceProvider
{
	public enum ClerkState { Working, GoingToBreak, OnBreak, ReturningToWork, GoingToToilet, AtToilet, Inactive, StressedOut, GoingToArchive, AtArchive, WaitingForArchive, ChairPatrol }
    public enum ClerkRole { Regular, Cashier, Registrar, Archivist }

    [Header("Настройки клерка")]
    public ClerkRole role = ClerkRole.Regular;
    public ServicePoint assignedServicePoint;
    public List<RoleData> allRoleData; // Убедитесь, что это поле есть и заполнено в инспекторе!

    // Поля для новых механик
    public float redirectionBonus = 0f;
    public bool IsDoingBooks = false;
    
    private ClerkState currentState = ClerkState.Inactive;
    private StackHolder stackHolder;

    public ClerkState GetCurrentState()
    {
        return currentState;
    }

    public void SetState(ClerkState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        logger?.LogState(GetStatusInfo());
        if(visuals != null)
        {
            visuals.SetEmotionForState(newState);
        }
    }

    public IEnumerator MoveToTarget(Vector2 targetPosition, ClerkState stateOnArrival)
    {
        agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, this.gameObject));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        SetState(stateOnArrival);
    }
    
    public void ServiceComplete()
    {
        currentFrustration += 0.05f; // Примерное значение выгорания за клиента
        if (assignedServicePoint != null && assignedServicePoint.documentStack != null)
        {
            assignedServicePoint.documentStack.AddDocumentToStack();
        }
    }

    public override bool IsOnBreak()
    {
        return currentState == ClerkState.OnBreak ||
               currentState == ClerkState.GoingToBreak || 
               currentState == ClerkState.AtToilet || 
               currentState == ClerkState.GoingToToilet ||
               currentState == ClerkState.StressedOut;
    }

    public override string GetStatusInfo()
    {
        return currentState.ToString();
    }
    
    public override string GetCurrentStateName()
    {
        return currentState.ToString();
    }
    
    public void InitializeFromData(RoleData data)
    {
        // ... (этот метод у вас уже есть, оставляем без изменений)
    }

    // >>> НАЧАЛО ВАЖНЫХ ИЗМЕНЕНИЙ <<<

    #region IServiceProvider Implementation

    public bool IsAvailableToServe => !IsOnBreak() && (currentState == ClerkState.Working || currentState == ClerkState.ChairPatrol);

    public Transform GetClientStandPoint()
    {
        return assignedServicePoint != null ? assignedServicePoint.clientStandPoint.transform : transform;
    }
    
    public ServicePoint GetWorkstation()
    {
        return assignedServicePoint;
    }

    // ОБНОВЛЕННЫЙ МЕТОД
    public void AssignClient(ClientPathfinding client)
    {
        // Если я кассир, запускаю свою логику
        if (this.role == ClerkRole.Cashier)
        {
            StartCoroutine(CashierServiceRoutine(client));
        }
        // Если я другая роль, ничего не делаю (логика в ClientStateMachine)
    }

    #endregion

    // НОВЫЙ МЕТОД ДЛЯ ЛОГИКИ КАССИРА
    private IEnumerator CashierServiceRoutine(ClientPathfinding client)
{
    SetState(ClerkState.Working);
    thoughtBubble?.ShowPriorityMessage($"К оплате: ${client.billToPay}", 3f, Color.white);
    yield return new WaitForSeconds(Random.Range(2f, 4f));

    int bill = client.billToPay;
    int totalSkimAmount = 0;

    // ... (код для получения настроек из RoleData) ...
    RoleData roleData = allRoleData.FirstOrDefault(d => d.roleType == currentRole);
    float corruptionChanceMult = 1.0f;
    float maxSkimAmount = 0.3f;
    if (roleData != null)
    {
        corruptionChanceMult = roleData.cashier_corruptionChanceMultiplier;
        maxSkimAmount = roleData.cashier_maxSkimAmount;
    }

    float corruptionChance = (skills.corruption * 0.5f) * corruptionChanceMult; 

    if (Random.value < corruptionChance && bill > 0)
    {
        totalSkimAmount = (int)(bill * Random.Range(0.1f, maxSkimAmount));
        thoughtBubble?.ShowPriorityMessage("Никто и не заметит...", 2f, new Color(0.8f, 0, 0.8f));
        yield return new WaitForSeconds(2f);
    }

    // >>> НАЧАЛО ИЗМЕНЕНИЙ <<<

    // 1. Сначала в казну идет официальная часть (сумма счета минус все, что украли)
    int officialAmount = bill - totalSkimAmount;
    if (officialAmount > 0)
    {
        PlayerWallet.Instance?.AddMoney(officialAmount, $"Оплата услуги (Клиент: {client.name})", IncomeType.Official);
    }

    // 2. Затем половина украденного идет на теневой счет игрока
    int playerSkimCut = totalSkimAmount / 2;
    if (playerSkimCut > 0)
    {
        PlayerWallet.Instance?.AddMoney(playerSkimCut, $"Доля от махинации ({name})", IncomeType.Shadow);
        Debug.LogWarning($"КАССИР {name} перевел на теневой счет {playerSkimCut}$ (Навык коррупции: {skills.corruption:P0})");
    }
    // Вторая половина просто "исчезает" - это доля кассира.

    // >>> КОНЕЦ ИЗМЕНЕНИЙ <<<

    if (client.paymentSound != null) AudioSource.PlayClipAtPoint(client.paymentSound, transform.position);
    client.billToPay = 0;

    // ... (код отправки клиента на выход и завершения обслуживания) ...
    client.isLeavingSuccessfully = true;
    client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
    client.stateMachine.SetGoal(ClientSpawner.Instance.exitWaypoint);
    client.stateMachine.SetState(ClientState.Leaving);
    ServiceComplete();
}
}
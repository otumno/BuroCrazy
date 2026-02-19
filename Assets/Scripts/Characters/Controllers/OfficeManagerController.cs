// Assets/Scripts/Characters/Controllers/OfficeManagerController.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Managers;
using Gameplay.Documents;
using Utilities;
using Enums;

[RequireComponent(typeof(AgentMover), typeof(CharacterStateLogger))]
public class OfficeManagerController : StaffController
{
    public enum ManagerState
    {
        Idle,
        MovingToDoc,
        CarryingDoc,
        RefillingWater,
        RefillingPaper,
        OnBreak,
        GoingToBreak,
        GoingToToilet,
        AtToilet,
        StressedOut
    }

    [Header("Состояние")]
    private ManagerState currentState = ManagerState.Idle;
    
    [Header("Ссылки на действие")]
    public StaffAction transportAction; 

    public void SetState(ManagerState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        logger?.LogState(currentState.ToString());
        if (visuals != null) visuals.SetEmotionForState(newState);
    }

    // --- ИСПРАВЛЕНИЕ ОШИБКИ CS1503 (Перегрузка метода для Enum) ---
    public IEnumerator MoveToTarget(Vector2 targetPosition, ManagerState stateOnArrival)
    {
        // Вызываем базовую логику движения
        agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, this.gameObject));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        // Устанавливаем наше типизированное состояние
        SetState(stateOnArrival);
    }

    protected override void SetArrivalState(string stateName)
    {
        if (System.Enum.TryParse<ManagerState>(stateName, out ManagerState newState))
        {
            SetState(newState);
        }
    }

    public override bool IsOnBreak()
    {
        return currentState == ManagerState.OnBreak || currentState == ManagerState.AtToilet;
    }

    public override void StartShift()
    {
        base.StartShift();
        StartCoroutine(ManagerLogicRoutine());
    }

    private IEnumerator ManagerLogicRoutine()
    {
        while (IsOnDuty())
        {
            if (currentExecutor != null || IsOnBreak()) { yield return new WaitForSeconds(1f); continue; }

            // Логистика документов
            if (TryFindDocumentJob(out DocumentStack source, out DocumentStack target, out ProjectDocumentObject doc))
            {
                // Если target == null, значит это ПОЛИТИКА (см. TryFindDocumentJob)
                if (target == null && doc.documentData.docType == ProjectDocumentType.Policy)
                {
                    yield return StartCoroutine(ExecutePolicyTransport(source, doc));
                }
                else
                {
                    ExecuteTransport(source, target, doc);
                }
            }
            // Ресурсы и прочее...
            else
            {
                // ... (код ресурсов, который мы писали ранее) ...
                 StaffAction bestSystemAction = null;
                var allActions = new List<StaffAction>(activeActions);
                if (systemActionDatabase != null) allActions.AddRange(systemActionDatabase.allActions);
                
                var replenishAction = allActions.FirstOrDefault(a => a.actionType == ActionType.ReplenishResource); // Или Find
                if (replenishAction != null && replenishAction.AreConditionsMet(this)) ExecuteAction(replenishAction);
                else SetState(ManagerState.Idle);
            }
            yield return new WaitForSeconds(2f);
        }
    }

    private bool TryFindDocumentJob(out DocumentStack source, out DocumentStack target, out ProjectDocumentObject foundDoc)
    {
        source = null; target = null; foundDoc = null;

        var allStacks = FindObjectsByType<DocumentStack>(FindObjectsSortMode.None);
        
        foreach (var stack in allStacks)
        {
            if (ArchiveManager.Instance != null && stack == ArchiveManager.Instance.mainDocumentStack) continue;

            // --- НОВОЕ: ПОЛИТИКИ (Высший приоритет) ---
            // Ищем документ: Тип Policy, Подписан Директором
            var policyDoc = stack.GetAllProjectDocuments().FirstOrDefault(d => 
                d.documentData.docType == ProjectDocumentType.Policy && 
                d.documentData.signedByDirector);

            if (policyDoc != null)
            {
                // Цель - Доска Указов (находим через Registry)
                var board = ScenePointsRegistry.Instance?.noticeBoard;
                if (board != null)
                {
                    // Мы не можем вернуть target как DocumentStack, потому что доска это NoticeBoard.
                    // Нам придется обработать это отдельно.
                    // Для совместимости метода, мы вернем source и doc, а target оставим null, 
                    // но обработаем это в вызывающем методе.
                    
                    source = stack; 
                    target = null; // Маркер того, что это особая цель
                    foundDoc = policyDoc;
                    return true;
                }
            }
            // ------------------------------------------

            // 1. Директор -> Регистратура (Стандарт)
            var docToRegistrar = stack.FindDoc(d => d.signedByDirector && !d.processedByRegistrar && d.docType != ProjectDocumentType.Policy);
            if (docToRegistrar != null)
            {
                var regStack = GetRegistrarStack();
                if (regStack != null && stack != regStack)
                {
                    source = stack; target = regStack; foundDoc = docToRegistrar;
                    return true;
                }
            }

            // 2. Регистратура -> БУХГАЛТЕР
            var docToAccountant = stack.FindDoc(d => d.processedByRegistrar && !d.paidAtCashier && d.docType != ProjectDocumentType.Policy);
            if (docToAccountant != null)
            {
                var accStack = GetAccountantStack();
                if (accStack != null && stack != accStack)
                {
                    source = stack; target = accStack; foundDoc = docToAccountant;
                    return true;
                }
            }

            // 3. Бухгалтер -> Архив
            var docToArchive = stack.FindDoc(d => d.paidAtCashier && !d.archived && d.docType != ProjectDocumentType.Policy);
            if (docToArchive != null)
            {
                if (ArchiveManager.Instance != null && stack != ArchiveManager.Instance.mainDocumentStack)
                {
                    source = stack; target = ArchiveManager.Instance.mainDocumentStack; foundDoc = docToArchive;
                    return true;
                }
            }
        }
        return false;
    }

    private void ExecuteTransport(DocumentStack source, DocumentStack target, ProjectDocumentObject doc)
    {
        if (transportAction == null)
        {
            transportAction = Resources.Load<StaffAction>("Actions/Action_TransportProjectDocument");
        }

        if (transportAction != null)
        {
            var executor = gameObject.AddComponent<TransportProjectDocumentExecutor>();
            executor.SourceStack = source;
            executor.TargetStack = target;
            executor.TargetDoc = doc;
            
            // Запускаем executor
            executor.Execute(this, transportAction);
        }
    }
	
	private IEnumerator ExecutePolicyTransport(DocumentStack sourceStack, ProjectDocumentObject doc)
    {
        var board = ScenePointsRegistry.Instance.noticeBoard;
        if (board == null) yield break;

        // 1. Идем к столу (где лежит указ)
        thoughtBubble?.ShowPriorityMessage("Новый указ!", 2f, Color.magenta);
        yield return StartCoroutine(MoveToTarget(sourceStack.transform.position, ManagerState.CarryingDoc));

        // 2. Берем
        var heldDoc = sourceStack.TakeSpecificDocument(doc);
        if (heldDoc != null)
        {
            heldDoc.transform.SetParent(transform);
            heldDoc.transform.localPosition = new Vector3(0, 0.5f, 0);
            
            // 3. Идем к Доске
            thoughtBubble?.ShowPriorityMessage("На всеобщее обозрение!", 2f, Color.magenta);
            yield return StartCoroutine(MoveToTarget(board.interactionPoint.position, ManagerState.CarryingDoc));

            // 4. Вешаем
            yield return new WaitForSeconds(0.5f);
            board.PinPolicy(heldDoc.GetComponent<ProjectDocumentObject>());
            
            ExperienceManager.Instance?.GrantXP(this, ActionType.TransportProjectDocument); // Даем опыт
        }
        SetState(ManagerState.Idle);
    }

    // Хелперы поиска целевых стопок
    private DocumentStack GetRegistrarStack() => ClientSpawner.GetZoneByDeskId(0)?.GetComponentInChildren<ServicePoint>()?.documentStack;
    private DocumentStack GetCashierStack() => ClientSpawner.GetZoneByDeskId(-1)?.GetComponentInChildren<ServicePoint>()?.documentStack;
	
	private DocumentStack GetAccountantStack()
    {
        // Ищем через реестр (ID стола бухгалтера, например, 4 или найти по имени)
        // В ScenePointsRegistry есть поле bookkeepingDesk
        return ScenePointsRegistry.Instance?.bookkeepingDesk?.documentStack;
    }
	
}

// Extension для удобства поиска (оставляем, он полезен)
public static class StackExtensions
{
    public static ProjectDocumentObject FindDoc(this DocumentStack stack, System.Func<Data.Documents.ProjectDocumentDefinition, bool> predicate)
    {
        var allDocs = stack.GetComponentsInChildren<ProjectDocumentObject>();
        return allDocs.FirstOrDefault(d => d.documentData != null && predicate(d.documentData));
    }
}
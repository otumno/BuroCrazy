// Файл: Assets/Scripts/Characters/Controllers/Actions/ActionExecutor.cs
using UnityEngine;
using System.Collections;

public abstract class ActionExecutor : MonoBehaviour
{
    public virtual bool IsInterruptible => true;
    protected StaffController staff;
    public StaffAction actionData { get; private set; }
    private bool _finished;
    private readonly System.Collections.Generic.List<System.Action> cleanup = new();

    // Cleanup runs synchronously before another order starts; Destroy runs only at frame end.
    protected void RegisterCleanup(System.Action release) => cleanup.Add(release);

    private void ReleaseResources()
    {
        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            try { cleanup[i](); }
            catch (System.Exception exception) { Debug.LogException(exception, this); }
        }
        cleanup.Clear();
        OnResourcesReleased();
        if (staff != null)
        {
            staff.GetComponent<StackHolder>()?.HideStack();
            staff.CurrentSubStatus = "";
        }
    }

    protected virtual void OnResourcesReleased() { }

    protected virtual void OnDestroy()
    {
        if (!_finished)
        {
            _finished = true;
            StopAllCoroutines();
            ReleaseResources();
        }
    }

    public void Execute(StaffController staff, StaffAction actionData)
    {
        this.staff = staff;
        this.actionData = actionData;
        StartCoroutine(ActionRoutine());
    }

    protected abstract IEnumerator ActionRoutine();

    protected IEnumerator ReturnToWorkstation()
    {
        var point = staff.assignedWorkstation != null ? staff.assignedWorkstation.clerkStandPoint : null;
        if (point == null) yield break;
        staff.CurrentSubStatus = "Возвращается на рабочее место";
        yield return staff.MoveToTarget(point.position, "Working");
        if (staff is IServiceProvider provider && Vector2.Distance(staff.transform.position, point.position) < 1f)
            Managers.ClientSpawner.AssignServiceProviderToDesk(provider, staff.assignedWorkstation.deskId);
        staff.CurrentSubStatus = "";
    }

    protected bool IsAtWorkstation => staff.assignedWorkstation != null && staff.assignedWorkstation.clerkStandPoint != null
        && Vector2.Distance(staff.transform.position, staff.assignedWorkstation.clerkStandPoint.position) < 1f;

    protected void ShowProcessing(Vector3 target, float duration)
    {
        var references = staff.GetComponent<StaffPrefabReferences>();
        if (references == null || references.processingIconPrefab == null) return;
        var icon = Instantiate(references.processingIconPrefab);
        RegisterCleanup(() => { if (icon != null) Destroy(icon); });
        icon.GetComponent<ProcessingAnimationUI>()?.Play(staff.transform.position, target, duration);
    }
    
    // ----- НОВЫЙ МЕТОД ЗАВЕРШЕНИЯ -----
    protected void FinishAction(bool success)
    {
        if (_finished) return;
        _finished = true;
        ReleaseResources();
        OnActionCompleted(success); // Вызываем новый метод для обратной связи

        if (staff != null)
        {
            // FIXED: Игнорируем базовые нужды
            bool isBasicNeed = actionData != null && (
                actionData.actionType == ActionType.GoHome ||
                actionData.actionType == ActionType.GoToToilet ||
                actionData.actionType == ActionType.GoToBreak ||
                actionData.actionType == ActionType.GoToCooler ||
                actionData.actionType == ActionType.Eat ||
                actionData.actionType == ActionType.Drink
            );

            if (!isBasicNeed)
            {
                staff.ShowActionEffect(success);

                // --- РЕГИСТРАЦИЯ РЕПУТАЦИИ (HP) ---
                if (Managers.DirectorManager.Instance != null)
                {
                    if (success) Managers.DirectorManager.Instance.RegisterSuccess(staff.currentRole);
                    else Managers.DirectorManager.Instance.RegisterFailure(staff.currentRole);
                }
            }
            
            // --- Логирование завершения задачи в ActionDiary ---
            if (actionData != null)
            {
                staff.GetComponent<ActionDiary>()?.LogEvent($"Завершил задачу: {actionData.displayName} (Успех: {success})");
            }

            staff.OnExecutorFinished(this, actionData, success);
        }
        Destroy(this);
    }

    protected virtual void OnActionCompleted(bool success)
    {
        bool isBasicNeed = actionData != null && (
            actionData.actionType == ActionType.GoHome ||
            actionData.actionType == ActionType.GoToToilet ||
            actionData.actionType == ActionType.GoToBreak ||
            actionData.actionType == ActionType.GoToCooler ||
            actionData.actionType == ActionType.Eat ||
            actionData.actionType == ActionType.Drink
        );
        if (isBasicNeed) return; // Не спамим "Готово!" для туалета

        // Базовая реализация: просто показывает мысль об успехе/провале
        if (success)
        {
            staff.thoughtBubble?.ShowPriorityMessage("Готово!", 2f, Color.green);
        }
        else
        {
            staff.thoughtBubble?.ShowPriorityMessage("Эх, не вышло...", 2f, Color.red);
        }
    }

    // --- НОВЫЙ МЕТОД ДЛЯ ПРЕРЫВАНИЯ ---
    public virtual void Interrupt()
    {
        if (_finished) return;
        _finished = true;
        StopAllCoroutines();
        ReleaseResources();
        
        if (staff != null)
        {
            staff.thoughtBubble?.ShowPriorityMessage("Бросаю всё!", 2f, Color.yellow);
            // Тормозим физику
            staff.AgentMover?.Stop();
            staff.OnExecutorFinished(this, actionData, false);
        }
        
        Destroy(this);
    }
}

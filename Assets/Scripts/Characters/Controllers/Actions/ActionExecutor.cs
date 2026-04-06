// Файл: Assets/Scripts/Characters/Controllers/Actions/ActionExecutor.cs
using UnityEngine;
using System.Collections;

public abstract class ActionExecutor : MonoBehaviour
{
    public virtual bool IsInterruptible => true;
    protected StaffController staff;
    public StaffAction actionData { get; private set; }

    public void Execute(StaffController staff, StaffAction actionData)
    {
        this.staff = staff;
        this.actionData = actionData;
        StartCoroutine(ActionRoutine());
    }

    protected abstract IEnumerator ActionRoutine();
    
    // ----- НОВЫЙ МЕТОД ЗАВЕРШЕНИЯ -----
    protected void FinishAction(bool success)
    {
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

            staff.OnActionFinished();
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
        StopAllCoroutines();
        staff.thoughtBubble?.ShowPriorityMessage("Бросаю всё!", 2f, Color.yellow);
        
        if (staff != null)
        {
            // Тормозим физику
            staff.AgentMover?.Stop();
            staff.OnActionFinished(actionData, false);
        }
        
        Destroy(this);
    }
}
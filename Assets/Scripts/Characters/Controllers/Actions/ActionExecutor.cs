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
            staff.OnActionFinished();
        }
        Destroy(this);
    }

    // ----- НОВЫЙ ВИРТУАЛЬНЫЙ МЕТОД ДЛЯ ОБРАТНОЙ СВЯЗИ -----
    protected virtual void OnActionCompleted(bool success)
    {
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
}
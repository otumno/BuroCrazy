// Файл: Assets/Scripts/Characters/Controllers/Actions/ActionExecutor.cs
using UnityEngine;
using System.Collections;

public abstract class ActionExecutor : MonoBehaviour
{
	public virtual bool IsInterruptible => true;
    protected StaffController staff;
    protected StaffAction actionData;

    // Главный метод, который будет запускать "мозг"
    public void Execute(StaffController staff, StaffAction actionData)
    {
        this.staff = staff;
        this.actionData = actionData;
        StartCoroutine(ActionRoutine());
    }

    // В этой корутине будет жить вся логика действия
    protected abstract IEnumerator ActionRoutine();

    // В конце каждого действия мы будем вызывать этот метод
    protected void FinishAction()
    {
        if (staff != null)
        {
            staff.OnActionFinished(); // Сообщаем "мозгу", что мы свободны
        }
        Destroy(this); // Самоуничтожаемся
    }
}
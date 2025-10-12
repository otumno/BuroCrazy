// Файл: Assets/Scripts/Characters/Controllers/Actions/ActionExecutor.cs
using UnityEngine;
using System.Collections;

public abstract class ActionExecutor : MonoBehaviour
{
    public virtual bool IsInterruptible => true;
    protected StaffController staff;

    // ----- НАЧАЛО ИЗМЕНЕНИЙ -----
    // Поле 'actionData' было 'protected StaffAction actionData;'
    // Теперь это публичное свойство с приватным сеттером.
    // Это позволяет другим скриптам (например, StaffController) ЧИТАТЬ его значение,
    // но только сам ActionExecutor может его ЗАПИСЫВАТЬ.
    public StaffAction actionData { get; private set; }
    // ----- КОНЕЦ ИЗМЕНЕНИЙ -----

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
            // Сообщаем "мозгу", что мы свободны
            staff.OnActionFinished();
        }
        // Самоуничтожаемся
        Destroy(this);
    }
}
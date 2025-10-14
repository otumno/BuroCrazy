// Файл: Assets/Scripts/Data/Actions/GoToToiletExecutor.cs
using UnityEngine;
using System.Collections;

public class GoToToiletExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Нельзя прерывать поход в туалет!

    protected override IEnumerator ActionRoutine()
    {
        Transform toiletPoint = ScenePointsRegistry.Instance?.staffToiletPoint;
        if (toiletPoint == null)
        {
            Debug.LogError($"[GoToToiletExecutor] Не найдена точка туалета (staffToiletPoint) в ScenePointsRegistry!");
            FinishAction();
            yield break;
        }

        staff.thoughtBubble?.ShowPriorityMessage("Нужно отойти...", 2f, Color.yellow);

        // --- Логика для разных типов сотрудников ---
        if (staff is ClerkController clerk)
        {
            clerk.SetState(ClerkController.ClerkState.GoingToToilet);
            yield return staff.StartCoroutine(staff.MoveToTarget(toiletPoint.position, ClerkController.ClerkState.AtToilet.ToString()));
            yield return new WaitForSeconds(10f); // Время в туалете
            clerk.SetState(ClerkController.ClerkState.ReturningToWork);
        }
        else if (staff is GuardMovement guard)
        {
            guard.SetState(GuardMovement.GuardState.GoingToToilet);
            yield return staff.StartCoroutine(staff.MoveToTarget(toiletPoint.position, GuardMovement.GuardState.AtToilet.ToString()));
            yield return new WaitForSeconds(10f);
            guard.SetState(GuardMovement.GuardState.Idle); // Возвращается в состояние бездействия
        }
        // ... здесь можно добавить логику для других типов сотрудников (Intern, ServiceWorker)
        
        staff.bladder = 0f; // Удовлетворяем потребность
        Debug.Log($"[AI Needs] {staff.name} сходил в туалет. Потребность 'Bladder' сброшена.");
        FinishAction();
    }
}
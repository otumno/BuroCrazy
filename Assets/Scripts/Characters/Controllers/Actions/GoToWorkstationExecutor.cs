using UnityEngine;
using System.Collections;

public class GoToWorkstationExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
{
    // --- НАЧАЛО ИЗМЕНЕНИЙ ---
    // Теперь мы проверяем новую переменную assignedWorkstation
    if (!(staff is ClerkController clerk) || clerk.assignedWorkstation == null) 
    { 
        // Если рабочее место не назначено, показываем мысль и завершаем действие,
        // чтобы AI мог попробовать сделать что-то еще (например, патрулировать).
        staff.thoughtBubble?.ShowPriorityMessage("Мне не назначили\nрабочее место!", 4f, Color.red);
        yield return new WaitForSeconds(5f);
        FinishAction();
        yield break; 
    }

    // Регистрируем клерка как активного работника на этой станции
    ClientSpawner.AssignServiceProviderToDesk(clerk, clerk.assignedWorkstation.deskId);

    clerk.SetState(ClerkController.ClerkState.ReturningToWork);

    // Идем к точке, указанной в новой переменной
    yield return staff.StartCoroutine(clerk.MoveToTarget(clerk.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working));

    // --- КОНЕЦ ИЗМЕНЕНИЙ ---

    // Бесконечно ждем, пока нас не прервет более важное дело
    while (true)
    {
        yield return new WaitForSeconds(5f);
    }
}
}
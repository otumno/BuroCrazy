// Файл: Assets/Scripts/Data/Actions/DirectorPrepareSalariesExecutor.cs
using UnityEngine;
using System.Collections;
using System.Linq;

public class DirectorPrepareSalariesExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        var director = staff as DirectorAvatarController;
        var bookkeepingDesk = ScenePointsRegistry.Instance?.bookkeepingDesk;
        if (director == null || bookkeepingDesk == null) { FinishAction(); yield break; }

        // ----- ИЗМЕНЕНИЕ ЗДЕСЬ -----
        // Мы преобразуем enum в строку с помощью .ToString()
        // Теперь вызов соответствует методу из базового класса StaffController
        yield return staff.StartCoroutine(director.MoveToTarget(bookkeepingDesk.clerkStandPoint.position, DirectorAvatarController.DirectorState.WorkingAtStation.ToString()));
        
        director.thoughtBubble?.ShowPriorityMessage("Придется самому...", 3f, Color.blue);

        var salaryStack = ScenePointsRegistry.Instance?.salaryStackPoint;
        if (salaryStack == null) { FinishAction(); yield break; }

        int envelopesToCreate = HiringManager.Instance.AllStaff.Count(s => s.unpaidPeriods > 0);
        envelopesToCreate -= salaryStack.CurrentEnvelopeCount;

        if (envelopesToCreate <= 0)
        {
            director.thoughtBubble?.ShowPriorityMessage("Зарплата уже готова.", 2f, Color.gray);
            FinishAction();
            yield break;
        }

        for (int i = 0; i < envelopesToCreate; i++)
        {
            yield return new WaitForSeconds(1.5f);
            if (!salaryStack.AddEnvelope())
            {
                director.thoughtBubble?.ShowPriorityMessage("Стол завален!", 2f, Color.red);
                break;
            }
        }

        FinishAction();
    }
}
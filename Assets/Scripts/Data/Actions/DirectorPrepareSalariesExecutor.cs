using UnityEngine;
using System.Collections;
using System.Linq;

public class DirectorPrepareSalariesExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Важное дело, не прерываем

    protected override IEnumerator ActionRoutine()
{
    var director = staff as DirectorAvatarController;
    // Находим стол бухгалтера через реестр
    var bookkeepingDesk = ScenePointsRegistry.Instance?.bookkeepingDesk; 

    if (director == null || bookkeepingDesk == null) { FinishAction(); yield break; }

    // 1. Идем к столу бухгалтерии
    yield return staff.StartCoroutine(director.MoveToTarget(bookkeepingDesk.clerkStandPoint.position, DirectorAvatarController.DirectorState.WorkingAtStation));

    director.thoughtBubble?.ShowPriorityMessage("Придется самому...", 3f, Color.blue);

    // 2. Считаем, сколько конвертов нужно
    // (Остальная логика метода остается без изменений, она будет работать с salaryStack, который мы найдем через ScenePointsRegistry)
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

        // 3. Создаем конверты
        for (int i = 0; i < envelopesToCreate; i++)
        {
            yield return new WaitForSeconds(1.5f); // Директор делает это быстрее кассира
            if (!salaryStack.AddEnvelope())
            {
                director.thoughtBubble?.ShowPriorityMessage("Стол завален!", 2f, Color.red);
                break;
            }
        }

        FinishAction();
    }
}
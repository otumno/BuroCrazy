using UnityEngine;
using System.Collections;
using System.Linq;

public class PrepareSalariesExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        var bookkeeper = staff as ClerkController;
        var bookkeepingDesk = ScenePointsRegistry.Instance?.bookkeepingDesk;
        var salaryStack = ScenePointsRegistry.Instance?.salaryStackPoint;
        if (bookkeeper == null || salaryStack == null || bookkeepingDesk == null) { FinishAction(); yield break; }

        // ----- THE FIX IS HERE -----
        yield return staff.StartCoroutine(bookkeeper.MoveToTarget(bookkeepingDesk.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString()));
        
        bookkeeper.thoughtBubble?.ShowPriorityMessage("Готовлю зарплату...", 3f, new Color(0.1f, 0.4f, 0.1f));
        bookkeeper.SetState(ClerkController.ClerkState.Working);

        int envelopesToCreate = 0;
        foreach (var employee in HiringManager.Instance.AllStaff)
        {
            if (employee.unpaidPeriods > 0)
            {
                envelopesToCreate++;
            }
        }

        envelopesToCreate -= salaryStack.CurrentEnvelopeCount;
        if (envelopesToCreate <= 0)
        {
            bookkeeper.thoughtBubble?.ShowPriorityMessage("Все уже готово.", 2f, Color.gray);
            FinishAction();
            yield break;
        }

        for (int i = 0; i < envelopesToCreate; i++)
        {
            yield return new WaitForSeconds(Random.Range(2f, 4f));
            if (salaryStack.AddEnvelope())
            {
                Debug.Log($"{bookkeeper.name} подготовил один зарплатный конверт.");
            }
            else
            {
                bookkeeper.thoughtBubble?.ShowPriorityMessage("Стол завален!", 2f, Color.red);
                Debug.LogWarning("Не удалось создать конверт: стопка зарплат переполнена.");
                break;
            }
        }

        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction();
    }
}
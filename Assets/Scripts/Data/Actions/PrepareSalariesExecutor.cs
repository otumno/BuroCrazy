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

    // --- НОВАЯ ЛОГИКА: Идем к столу бухгалтера ---
    yield return staff.StartCoroutine(bookkeeper.MoveToTarget(bookkeepingDesk.clerkStandPoint.position, ClerkController.ClerkState.Working));

    bookkeeper.thoughtBubble?.ShowPriorityMessage("Готовлю зарплату...", 3f, new Color(0.1f, 0.4f, 0.1f));
        bookkeeper.SetState(ClerkController.ClerkState.Working); // или Bookkeeping

        // 1. Считаем, сколько всего конвертов нужно создать
        int envelopesToCreate = 0;
        foreach (var employee in HiringManager.Instance.AllStaff)
        {
            if (employee.unpaidPeriods > 0)
            {
                envelopesToCreate++;
            }
        }

        // Вычитаем те, что уже лежат в стопке
        envelopesToCreate -= salaryStack.CurrentEnvelopeCount;

        if (envelopesToCreate <= 0)
        {
            bookkeeper.thoughtBubble?.ShowPriorityMessage("Все уже готово.", 2f, Color.gray);
            FinishAction();
            yield break;
        }

        // 2. Создаем конверты (без списания денег)
        for (int i = 0; i < envelopesToCreate; i++)
        {
            // Тратим время на "создание" одного конверта
            yield return new WaitForSeconds(Random.Range(2f, 4f));

            if (salaryStack.AddEnvelope())
            {
                Debug.Log($"{bookkeeper.name} подготовил один зарплатный конверт.");
            }
            else
            {
                bookkeeper.thoughtBubble?.ShowPriorityMessage("Стол завален!", 2f, Color.red);
                Debug.LogWarning("Не удалось создать конверт: стопка зарплат переполнена.");
                break; // Прерываем, если в стопке нет места
            }
        }

        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction();
    }
}
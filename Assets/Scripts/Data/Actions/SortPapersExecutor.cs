using UnityEngine;
using System.Collections;

public class SortPapersExecutor : ActionExecutor
{
    // Это рутинное действие, его можно прервать в любой момент, если появится клиент
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ClerkController clerk))
        {
            FinishAction();
            yield break;
        }

        // --- Логика действия ---

        // 1. Показываем мысль и устанавливаем состояние
        clerk.thoughtBubble?.ShowPriorityMessage("Надо бы прибраться...", 3f, Color.gray);
        // Можно добавить новое состояние, например, ClerkState.SortingPapers,
        // или просто оставить Working, так как это фоновая задача. Пока оставим Working.

        // 2. Ждем от 5 до 10 секунд, имитируя работу
        yield return new WaitForSeconds(Random.Range(5f, 10f));

        // 3. Даем бонус: небольшое снижение выгорания
        float frustrationRelief = 0.05f; // Снижаем на 5%
        float newFrustration = staff.GetCurrentFrustration() - frustrationRelief;
        staff.SetCurrentFrustration(newFrustration);

        Debug.Log($"{staff.name} отсортировал бумаги. Выгорание снижено на {frustrationRelief:P0}.");

        // 4. Начисляем немного опыта и завершаем
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction();
    }
}
using UnityEngine;
using System.Collections;

public class ChairPatrolExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;
    private ClerkController registrar;

    protected override IEnumerator ActionRoutine()
    {
        registrar = staff as ClerkController;
        if (registrar == null) { FinishAction(); yield break; }

        // Устанавливаем бонус, пока действие активно
        registrar.redirectionBonus = 0.25f; // Бонус 25%
        registrar.thoughtBubble?.ShowPriorityMessage("Готов к работе...", 5f, Color.gray);

        // Это "вечное" действие, которое прервется, как только появится более приоритетная задача (например, придет клиент)
        while (true)
        {
            yield return new WaitForSeconds(5f); // Просто ждем
        }
    }

    // Этот метод автоматически вызовется, когда действие прервется или завершится
    private void OnDestroy()
    {
        // ОБЯЗАТЕЛЬНО сбрасываем бонус, когда действие прекращается!
        if (registrar != null)
        {
            registrar.redirectionBonus = 0f;
        }
    }
}
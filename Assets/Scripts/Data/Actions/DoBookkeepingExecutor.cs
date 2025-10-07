using UnityEngine;
using System.Collections;

public class DoBookkeepingExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;
    private ClerkController bookkeeper;

    protected override IEnumerator ActionRoutine()
{
    bookkeeper = staff as ClerkController;
    var bookkeepingDesk = ScenePointsRegistry.Instance?.bookkeepingDesk;
    if (bookkeeper == null || bookkeepingDesk == null) { FinishAction(); yield break; }

    // --- НОВАЯ ЛОГИКА: Идем к столу бухгалтера ---
    yield return staff.StartCoroutine(bookkeeper.MoveToTarget(bookkeepingDesk.clerkStandPoint.position, ClerkController.ClerkState.Working));

    // Устанавливаем флаг, который сможет прочитать UI
    bookkeeper.IsDoingBooks = true;
        bookkeeper.SetState(ClerkController.ClerkState.Working); // или можно добавить новое состояние Bookkeeping
        bookkeeper.thoughtBubble?.ShowPriorityMessage("Свожу дебет с кредитом...", 10f, Color.gray);

        // "Вечное" действие, пока не прервут
        while (true)
        {
            // Каждые 15 секунд есть шанс найти "лишние" деньги
            yield return new WaitForSeconds(15f);
            if (Random.value < 0.1f) // 10% шанс
            {
                int bonus = Random.Range(20, 101);
                PlayerWallet.Instance?.AddMoney(bonus, staff.transform.position);
                bookkeeper.thoughtBubble?.ShowPriorityMessage($"Нашел лишние {bonus}$!", 4f, Color.green);
                Debug.Log($"{staff.name} нашел финансовую нестыковку на {bonus}$ в пользу заведения.");
            }
        }
    }

    private void OnDestroy()
    {
        // Когда действие прерывается, ОБЯЗАТЕЛЬНО сбрасываем флаг
        if (bookkeeper != null)
        {
            bookkeeper.IsDoingBooks = false;
        }
    }
}
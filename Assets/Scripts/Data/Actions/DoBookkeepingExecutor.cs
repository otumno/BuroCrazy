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

        // ----- THE FIX IS HERE -----
        yield return staff.StartCoroutine(bookkeeper.MoveToTarget(bookkeepingDesk.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString()));
        
        bookkeeper.IsDoingBooks = true;
        bookkeeper.SetState(ClerkController.ClerkState.Working);
        bookkeeper.thoughtBubble?.ShowPriorityMessage("Свожу дебет с кредитом...", 10f, Color.gray);
        
        while (true)
        {
            yield return new WaitForSeconds(15f);
            if (Random.value < 0.1f)
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
        if (bookkeeper != null)
        {
            bookkeeper.IsDoingBooks = false;
        }
    }
}
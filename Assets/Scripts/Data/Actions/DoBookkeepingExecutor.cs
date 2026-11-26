// Assets/Scripts/Data/Actions/DoBookkeepingExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;

public class DoBookkeepingExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;
    private ClerkController bookkeeper;
    private bool bonusApplied;

    protected override IEnumerator ActionRoutine()
    {
        bookkeeper = staff as ClerkController;
        // Используем стол бухгалтера из реестра
        var bookkeepingDesk = bookkeeper?.assignedWorkstation;
        
        if (bookkeeper == null || bookkeepingDesk == null || bookkeepingDesk.clerkStandPoint == null ||
            !actionData.AreConditionsMet(staff))
        {
            FinishAction(false); 
            yield break; 
        }

        // Идем к столу
        yield return bookkeeper.MoveToTarget(bookkeepingDesk.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString());
        if (Vector2.Distance(bookkeeper.transform.position, bookkeepingDesk.clerkStandPoint.position) > 1f)
        { FinishAction(false); yield break; }
        
        bookkeeper.IsDoingBooks = true;
        bookkeeper.SetState(ClerkController.ClerkState.Working);
        bookkeeper.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.accounts.taxes"), 4f, Color.gray);
        
        // Включаем бонус!
        // Чем выше навык PaperworkMastery, тем больше бонус (от 5% до 15%)
        float bonus = 0.05f + (bookkeeper.skills.paperworkMastery * 0.1f);
        if (PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.SetBookkeeperBonus(this, bonus);
            bonusApplied = true;
        }

        while (bookkeeper.assignedWorkstation == bookkeepingDesk && actionData.AreConditionsMet(staff))
        {
            // Каждые 5 секунд обновляем/проверяем
            yield return new WaitForSeconds(5f);
            
            if (bookkeeper.assignedWorkstation != bookkeepingDesk || !actionData.AreConditionsMet(staff)) break;
            // Маленький шанс найти "заначку"
            if (Random.value < 0.05f)
            {
                int foundMoney = Random.Range(10, 50);
                PlayerWallet.Instance?.AddMoney(foundMoney, UI.GameText.Get("ledger.accountant_find"), IncomeType.Shadow);
                bookkeeper.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.accounts.extra"), 2f, Color.green);
            }
        }
        FinishAction(true);
    }

    protected override void OnResourcesReleased()
    {
        // Когда действие прерывается (например, пошел спать или принесли папку), 
        // ВЫКЛЮЧАЕМ бонус
        if (bonusApplied && PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.SetBookkeeperBonus(this, 0f);
        }
        bonusApplied = false;

        if (bookkeeper != null)
        {
            bookkeeper.IsDoingBooks = false;
        }
    }
}

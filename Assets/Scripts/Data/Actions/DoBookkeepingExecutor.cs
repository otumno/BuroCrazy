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
        var bookkeepingDesk = ScenePointsRegistry.Instance?.bookkeepingDesk;
        
        if (bookkeeper == null || bookkeepingDesk == null || bookkeepingDesk.clerkStandPoint == null ||
            bookkeeper.assignedWorkstation != bookkeepingDesk)
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
        bookkeeper.thoughtBubble?.ShowPriorityMessage("Оптимизирую налоги...", 4f, Color.gray);
        
        // Включаем бонус!
        // Чем выше навык PaperworkMastery, тем больше бонус (от 5% до 15%)
        float bonus = 0.05f + (bookkeeper.skills.paperworkMastery * 0.1f);
        if (PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.SetAccountantBonus(bonus);
            bonusApplied = true;
        }

        while (true)
        {
            // Каждые 5 секунд обновляем/проверяем
            yield return new WaitForSeconds(5f);
            
            // Маленький шанс найти "заначку"
            if (Random.value < 0.05f)
            {
                int foundMoney = Random.Range(10, 50);
                PlayerWallet.Instance?.AddMoney(foundMoney, "Находка бухгалтера", IncomeType.Shadow);
                bookkeeper.thoughtBubble?.ShowPriorityMessage("Хм, лишние деньги!", 2f, Color.green);
            }
        }
    }

    protected override void OnResourcesReleased()
    {
        // Когда действие прерывается (например, пошел спать или принесли папку), 
        // ВЫКЛЮЧАЕМ бонус
        if (bonusApplied && PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.SetAccountantBonus(0f);
        }
        bonusApplied = false;

        if (bookkeeper != null)
        {
            bookkeeper.IsDoingBooks = false;
        }
    }
}

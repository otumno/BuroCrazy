// Assets/Scripts/Data/Actions/DirectorPrepareSalariesExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;

public class DirectorPrepareSalariesExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        // Получаем точку стопки (теперь это EnvelopeStack)
        var envelopeStack = ScenePointsRegistry.Instance?.salaryStackPoint; 
        
        if (envelopeStack == null)
        {
            staff.thoughtBubble?.ShowPriorityMessage("Нет места для зарплат!", 3f, Color.red);
            FinishAction(false);
            yield break;
        }

        // Идем к сейфу
        yield return staff.StartCoroutine(staff.MoveToTarget(envelopeStack.transform.position, "Working"));

        int costPerEnvelope = 100; // Условная цена
        int envelopesToMake = 5;

        for (int i = 0; i < envelopesToMake; i++)
        {
            if (PlayerWallet.Instance.GetCurrentMoney() < costPerEnvelope)
            {
                staff.thoughtBubble?.ShowPriorityMessage("Денег нет!", 2f, Color.red);
                break;
            }

            // ИСПРАВЛЕНИЕ: Вызываем метод у компонента, а не у трансформа
            if (envelopeStack.CurrentEnvelopeCount >= envelopeStack.maxCapacity)
            {
                staff.thoughtBubble?.ShowPriorityMessage("Сейф полон!", 2f, Color.red);
                break;
            }

            yield return new WaitForSeconds(1.5f);
            
            PlayerWallet.Instance.AddMoney(-costPerEnvelope, "Подготовка зарплаты");
            envelopeStack.AddEnvelope(); // Исправлено
            
            staff.thoughtBubble?.ShowPriorityMessage("+1 Конверт", 1f, Color.green);
        }

        FinishAction(true);
    }
}
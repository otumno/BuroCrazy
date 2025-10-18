using UnityEngine;
using System.Collections;

public class PrioritizeConsultationExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Это быстрое действие, прерывать нет смысла

    protected override IEnumerator ActionRoutine()
    {
        // Проверяем, что сотрудник может оказывать услуги (является IServiceProvider)
        if (!(staff is IServiceProvider provider)) 
        { 
            FinishAction(false); 
            yield break; 
        }

        staff.thoughtBubble?.ShowPriorityMessage("Кто просто спросить?", 2f, Color.green);
        
        // Вызываем метод в менеджере очереди, чтобы он нашел и позвал нужного клиента
        bool success = ClientQueueManager.Instance.CallClientWithSpecificGoal(ClientGoal.AskAndLeave, provider);
        
        if (success)
        {
            ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        }

        // Это "мгновенное" действие, даем небольшую паузу и завершаем
        yield return new WaitForSeconds(1f);
        FinishAction(success);
    }
}
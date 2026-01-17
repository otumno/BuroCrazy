// Assets/Scripts/Data/Actions/GoToCoolerExecutor.cs
using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Gameplay; 

public class GoToCoolerExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        // 1. Ищем кулер (код тот же)
        var cooler = Object.FindObjectsByType<ResourceContainer>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c.resourceType == ResourceType.Water);

        if (cooler == null) { FinishAction(false); yield break; }

        // 2. Идем (код тот же)
        if (staff is ClerkController clerk) {
            clerk.SetState(ClerkController.ClerkState.GoingToBreak);
            yield return staff.StartCoroutine(staff.MoveToTarget(cooler.transform.position, ClerkController.ClerkState.OnBreak.ToString()));
        } else {
            yield return staff.StartCoroutine(staff.MoveToTarget(cooler.transform.position, "Idle")); 
        }

        // 3. Пьем воду (код тот же + потребление)
        if (!cooler.IsEmpty) {
            cooler.Consume();
            staff.thoughtBubble?.ShowPriorityMessage("*Глыг-глыг*", 2f, Color.blue);
            staff.energy = 1f;
            staff.bladder = Mathf.Clamp01(staff.bladder + 0.3f);
        } else {
            staff.thoughtBubble?.ShowPriorityMessage("Пусто...", 2f, Color.red);
            staff.frustration += 0.1f;
        }

        // --- 4. СОЦИАЛИЗАЦИЯ (ОБНОВЛЕНО) ---
        var talker = staff.GetComponent<SmallTalkController>();
        
        // Ищем коллегу рядом
        var colleague = Physics2D.OverlapCircleAll(staff.transform.position, 2.0f)
            .Select(c => c.GetComponent<StaffController>())
            .FirstOrDefault(s => s != null && s != staff && s.IsOnDuty());

        if (colleague != null && talker != null)
        {
            // Начинаем "диалог" (обмен 2-3 репликами)
            int turns = Random.Range(2, 4); 
            
            for (int i = 0; i < turns; i++)
            {
                // Моя реплика
                string myPhrase = talker.GetRandomTopic();
                talker.ForceSayPhrase(myPhrase);
                
                yield return new WaitForSeconds(2.5f); // Ждем пока скажет

                // Ответ коллеги (если у него есть такой же контроллер)
                var colleagueTalker = colleague.GetComponent<SmallTalkController>();
                if (colleagueTalker != null)
                {
                    string reply = colleagueTalker.GetRandomTopic();
                    colleagueTalker.ForceSayPhrase(reply);
                }
                
                yield return new WaitForSeconds(2.5f); // Ждем ответа

                // Эффект от разговора (снижаем стресс каждую фразу)
                staff.morale = 1f;
                staff.frustration = Mathf.Max(0, staff.frustration - 0.15f);
                
                // Проверка, не ушел ли коллега
                if (Vector2.Distance(staff.transform.position, colleague.transform.position) > 3f) break;
            }
        }
        else
        {
            yield return new WaitForSeconds(2f); // Просто стоим пьем
        }
        // -----------------------------------

        if (staff is ClerkController c) c.SetState(ClerkController.ClerkState.ReturningToWork);
        FinishAction(true);
    }
}
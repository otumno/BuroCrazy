using UnityEngine;
using System.Collections;
using System.Linq;

public class CheckDocumentExecutor : ActionExecutor
{
    // Это действие нельзя прерывать, пока клерк не закончит проверку.
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        // Убеждаемся, что исполнитель - это Клерк.
        if (!(staff is ClerkController clerk))
        {
            FinishAction();
            yield break;
        }

        // --- Шаг 1: Найти клиента, которого мы обслуживаем ---
        var servicePoint = clerk.assignedServicePoint;
        if (servicePoint == null)
        {
            FinishAction(); // Если клерк не приписан к столу, он не может работать.
            yield break;
        }
        
        // Находим зону, к которой относится наш стол
        var zone = ClientSpawner.GetZoneByDeskId(servicePoint.deskId);
        if (zone == null)
        {
            FinishAction();
            yield break;
        }

        // Находим клиента, который занимает место в этой зоне.
        ClientPathfinding client = zone.GetOccupyingClients().FirstOrDefault();
        if (client == null)
        {
            // Если клиента нет, то и проверять нечего. Завершаем действие.
            FinishAction();
            yield break;
        }
        
        // --- Шаг 2: Выполнение логики проверки ---
        
        // Имитируем процесс проверки (думаем 1-3 секунды)
        clerk.thoughtBubble?.ShowPriorityMessage("Проверяю...", 2f, Color.yellow);
        yield return new WaitForSeconds(Random.Range(1f, 3f));

        float documentErrorPercent = (1f - client.documentQuality) * 100f;
        bool errorFound = false;

        // Если в документе есть ошибки (больше 10%)
        if (documentErrorPercent > 10f)
        {
            // Определяем шанс заметить ошибку на основе навыка "Педантичность"
            float chanceToSpotErrors = clerk.skills != null ? clerk.skills.pedantry : 0.5f;
            if (Random.value < chanceToSpotErrors)
            {
                errorFound = true;
            }
        }
        
        // --- Шаг 3: Результат проверки ---

        if (errorFound)
        {
            // ОШИБКА НАЙДЕНА
            Debug.Log($"<color=orange>Клерк {clerk.name} нашел ошибку в документе клиента {client.name}.</color>");
            clerk.thoughtBubble?.ShowPriorityMessage("Здесь ошибка!\nНужно переделать.", 3f, Color.red);
            yield return new WaitForSeconds(2f);
            
            // Отправляем клиента за новым бланком
            client.stateMachine.GoGetFormAndReturn();
        }
        else
        {
            // ОШИБОК НЕТ (или клерк их не заметил)
            Debug.Log($"<color=green>Клерк {clerk.name} проверил документ клиента {client.name}. Ошибок не найдено.</color>");
            clerk.thoughtBubble?.ShowPriorityMessage("Все в порядке.", 2f, Color.green);
            
            // Помечаем, что документ прошел проверку. Это КЛЮЧЕВОЙ момент для следующего действия.
            client.documentChecked = true; 
        }

        // Начисляем опыт за выполненное действие
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);

        // --- Шаг 4: Завершение действия ---
        FinishAction();
    }
}
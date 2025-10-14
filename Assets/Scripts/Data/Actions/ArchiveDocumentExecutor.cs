// Файл: Assets/Scripts/Data/Actions/ArchiveDocumentExecutor.cs
using UnityEngine;
using System.Collections;
using System.Linq; // Добавлено для Linq

public class ArchiveDocumentExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        var archivist = staff;
        if (archivist == null) { FinishAction(); yield break; }

        if (!ArchiveManager.Instance.mainDocumentStack.TakeOneDocument())
        {
            FinishAction();
            yield break;
        }

        var stackHolder = archivist.GetComponent<StackHolder>();
        stackHolder?.ShowSingleDocumentSprite();

        // --- НОВАЯ ЛОГИКА: ПРОВЕРКА НАВЫКА ИСПРАВЛЕНИЯ ОШИБОК ---
        bool canCorrectDocuments = archivist.activeActions.Any(a => a.actionType == ActionType.CorrectDocument);
        if (canCorrectDocuments && DocumentQualityManager.Instance.GetCurrentAverageErrorRate() > 0)
        {
            // Шанс на исправление зависит от Педантичности
            float correctionChance = 0.1f + (archivist.skills.pedantry * 0.4f); // от 10% до 50%
            if (Random.value < correctionChance)
            {
                archivist.thoughtBubble?.ShowPriorityMessage("Тут ошибка... исправлю.", 3f, Color.yellow);
                yield return new WaitForSeconds(Random.Range(3f, 5f)); // Дополнительное время на исправление

                float correctionStrength = 0.2f + (archivist.skills.paperworkMastery * 0.5f);
                if (DocumentQualityManager.Instance.CorrectWorstDocument(correctionStrength))
                {
                    Debug.Log($"{archivist.name} исправил ошибки в документе во время архивации.");
                }
            }
        }
        // --- КОНЕЦ НОВОЙ ЛОГИКИ ---

        var targetCabinet = ArchiveManager.Instance.GetRandomCabinet();
        if (targetCabinet == null) { FinishAction(); yield break; }

        archivist.thoughtBubble?.ShowPriorityMessage("Архивирую...", 3f, Color.gray);
        archivist.AgentMover.SetPath(PathfindingUtility.BuildPathTo(staff.transform.position, targetCabinet.transform.position, staff.gameObject));
        yield return new WaitUntil(() => !archivist.AgentMover.IsMoving());

        yield return new WaitForSeconds(Random.Range(2f, 4f));
        stackHolder?.HideStack();

        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction();
    }
}
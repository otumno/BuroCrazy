using UnityEngine;
using System.Collections;

public class CorrectDocumentExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        var archivist = staff;
        if (archivist == null) { FinishAction(); yield break; }

        archivist.thoughtBubble?.ShowPriorityMessage("Ищу ошибки...", 2f, Color.yellow);
        yield return new WaitForSeconds(Random.Range(4f, 7f)); // Поиск и исправление требуют времени

        // Сила исправления зависит от навыка "Бюрократия"
        float correctionStrength = 0.2f + (archivist.skills.paperworkMastery * 0.5f); // от 20% до 70%

        if (DocumentQualityManager.Instance.CorrectWorstDocument(correctionStrength))
        {
            Debug.Log($"{archivist.name} исправил ошибки в документе. Сила исправления: {correctionStrength:P0}");
            ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        }

        FinishAction();
    }
}
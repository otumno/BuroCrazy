using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_DeliverDocuments", menuName = "Bureau/Actions/DeliverDocuments")]
public class DeliverDocumentsAction : StaffAction
{
    [Tooltip("Насколько должна быть заполнена стопка (в процентах), чтобы стажер обратил на нее внимание.")]
    [Range(0.1f, 1f)]
    public float stackFullnessThreshold = 0.5f; // 50% по умолчанию

    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is InternController intern) || intern.IsOnBreak())
        {
            return false;
        }

        // Ищем все стопки документов на сцене.
        var allStacks = Object.FindObjectsByType<DocumentStack>(FindObjectsSortMode.None);

        // Проверяем, есть ли ХОТЯ БЫ ОДНА стопка, которая:
        // 1. Не является главной стопкой в архиве.
        // 2. Заполнена больше, чем наш порог.
        return allStacks.Any(stack => 
            stack != ArchiveManager.Instance.mainDocumentStack && 
            (float)stack.CurrentSize / stack.maxStackSize >= stackFullnessThreshold);
    }

    public override System.Type GetExecutorType()
    {
        return typeof(DeliverDocumentsExecutor);
    }
}
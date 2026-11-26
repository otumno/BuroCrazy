using UnityEngine;
using System.Linq;
using Managers;

[CreateAssetMenu(fileName = "Action_DeliverDocuments", menuName = "Bureau/Actions/DeliverDocuments")]
public class DeliverDocumentsAction : StaffAction
{
    [Tooltip("Насколько должна быть заполнена стопка (в процентах), чтобы стажер обратил на нее внимание.")]
    [Range(0.1f, 1f)]
    public float stackFullnessThreshold = 0.5f; // 50% по умолчанию

    public override bool AreConditionsMet(StaffController staff)
    {
        if (staff == null || (!(staff is InternController) && !(staff is OfficeManagerController) && staff.currentRole != StaffController.Role.Registrar) || staff.IsOnBreak() ||
            ArchiveManager.Instance == null || ScenePointsRegistry.Instance == null)
        {
            return false;
        }

        // Ищем все стопки документов на сцене.
        var allStacks = Object.FindObjectsByType<DocumentStack>(FindObjectsSortMode.None);

        // Проверяем, есть ли ХОТЯ БЫ ОДНА стопка, которая:
        // 1. Не является главной стопкой в архиве.
        // 2. Заполнена больше, чем наш порог.
        return allStacks.Any(stack => (staff.currentRole != StaffController.Role.Registrar ||
            staff.assignedWorkstation != null && staff.assignedWorkstation.CurrentClient == null && staff.assignedWorkstation.documentStack == stack) &&
            !ArchiveManager.Instance.OwnsStack(stack) && stack.OrdinaryDocumentCount > 0 &&
            (float)stack.OrdinaryDocumentCount / Mathf.Max(1, stack.maxStackSize) >= stackFullnessThreshold &&
            ScenePointsRegistry.Instance.allServicePoints.Any(point => point != null && point.documentStack == stack && point.internCollectionPoint != null));
    }

    public override System.Type GetExecutorType()
    {
        return typeof(DeliverDocumentsExecutor);
    }
}

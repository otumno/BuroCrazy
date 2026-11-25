// Assets/Scripts/Data/Actions/TransportProjectDocumentAction.cs
using UnityEngine;
using Managers;

[CreateAssetMenu(fileName = "Action_TransportProjectDocument", menuName = "Bureau/Actions/TransportProjectDocument")]
public class TransportProjectDocumentAction : StaffAction
{
    public TransportProjectDocumentAction()
    {
        category = ActionCategory.System;
        priority = 20; // Очень высокий приоритет (важнее болтовни и еды)
    }

    // Это действие запускается контроллером напрямую, условия проверяются в "мозге" менеджера
    public override bool AreConditionsMet(StaffController staff)
    {
        // Only OfficeManagerController can supply SourceStack/TargetDoc. A generic AI
        // launch creates an empty executor and repeatedly penalizes a failed task.
        return false;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(TransportProjectDocumentExecutor);
    }
}

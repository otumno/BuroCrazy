using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_ProcessDocCat1", menuName = "Bureau/Actions/ProcessDocumentCat1")]
public class ProcessDocumentCat1Action : StaffAction
{
    // Это действие теперь является "навыком", а не самостоятельной задачей.
    public override bool AreConditionsMet(StaffController staff)
    {
        return false;
    }

    public override System.Type GetExecutorType()
    {
        // Исполнитель все еще нужен, но он будет вызываться из ServiceAtOfficeDeskExecutor
        return typeof(ProcessDocumentCat1Executor);
    }
}
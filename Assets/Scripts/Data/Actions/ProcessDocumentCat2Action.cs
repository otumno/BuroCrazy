using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_ProcessDocCat2", menuName = "Bureau/Actions/ProcessDocumentCat2")]
public class ProcessDocumentCat2Action : StaffAction
{
    // Это действие теперь является "навыком", а не самостоятельной задачей.
    public override bool AreConditionsMet(StaffController staff)
    {
        return false;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ProcessDocumentCat2Executor);
    }
}
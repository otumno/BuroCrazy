using UnityEngine;

[CreateAssetMenu(fileName = "Action_ReturnToHomeBase", menuName = "Bureau/Actions/ReturnToHomeBase")]
public class ReturnToHomeBaseAction : StaffAction
{
    // Условия для этого действия всегда выполнены, так как это "задача по умолчанию"
    public override bool AreConditionsMet(StaffController staff)
    {
        return staff is ServiceWorkerController;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ReturnToHomeBaseExecutor);
    }
}
using UnityEngine;

[CreateAssetMenu(fileName = "Action_DirectorPrepareSalaries", menuName = "Bureau/Actions/DirectorPrepareSalaries")]
public class DirectorPrepareSalariesAction : StaffAction
{
    // Это действие запускается вручную, поэтому AI его выбирать не должен.
    // Условие всегда ложно, чтобы "мозг" персонажа его игнорировал.
    public override bool AreConditionsMet(StaffController staff)
    {
        return false;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(DirectorPrepareSalariesExecutor);
    }
}
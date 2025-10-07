using UnityEngine;

[CreateAssetMenu(fileName = "Action_FindValuables", menuName = "Bureau/Actions/FindValuables")]
public class FindValuablesAction : StaffAction
{
    // Это действие не может быть запущено само по себе, оно лишь флаг
    public override bool AreConditionsMet(StaffController staff) { return false; }
    public override System.Type GetExecutorType() { return null; }
}
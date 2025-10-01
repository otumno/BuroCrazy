using UnityEngine;

[CreateAssetMenu(fileName = "Action_CalmDownViolator", menuName = "Bureau/Actions/CalmDownViolator")]
public class CalmDownViolatorAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // Проверяем, что это охранник и он НЕ на перерыве/в туалете
        if (!(staff is GuardMovement guard) || guard.IsOnBreak())
        {
            return false;
        }

        // Главное условие: есть ли в данный момент нарушитель, которого нужно успокоить?
        return GuardManager.Instance != null && GuardManager.Instance.GetViolatorToHandle() != null;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(CalmDownViolatorExecutor);
    }
}
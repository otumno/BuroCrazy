using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_CoverCashier", menuName = "Bureau/Actions/CoverCashier")]
public class CoverCashierAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // Условие 1: Это должен быть стажер, и он не должен быть на перерыве.
        if (!(staff is InternController intern) || intern.IsOnBreak())
        {
            return false;
        }

        // Условие 2: Найти хотя бы одно рабочее место Регистратора (deskId = 0), которое сейчас не занято.
        return ScenePointsRegistry.Instance.allServicePoints
            .Any(p => p.deskId == -1 && ClientSpawner.GetServiceProviderAtDesk(p.deskId) == null);
    }

    public override System.Type GetExecutorType() { return typeof(CoverDeskExecutor); }
}
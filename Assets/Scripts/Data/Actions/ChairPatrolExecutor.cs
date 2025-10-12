// Файл: Assets/Scripts/Characters/Controllers/Actions/ChairPatrolExecutor.cs
using UnityEngine;
using System.Collections;
using System.Linq;

public class ChairPatrolExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;
    private ClerkController registrar;

    protected override IEnumerator ActionRoutine()
    {
        registrar = staff as ClerkController;
        if (registrar == null || registrar.assignedWorkstation == null) { FinishAction(); yield break; }

        registrar.SetState(ClerkController.ClerkState.ChairPatrol);
        registrar.redirectionBonus = 0.25f;
        registrar.thoughtBubble?.ShowPriorityMessage("Готов к работе...", 5f, Color.gray);

        while (true)
        {
            var zone = ClientSpawner.GetZoneByDeskId(registrar.assignedWorkstation.deskId);
            
            if (zone != null && zone.GetOccupyingClients().Any())
            {
                Debug.Log($"[ChairPatrol] {registrar.name} увидел клиента и завершает патруль, чтобы начать обслуживание.");
                break;
            }

            yield return new WaitForSeconds(1f);
        }
        
        FinishAction();
    }

    private void OnDestroy()
    {
        if (registrar != null)
        {
            registrar.SetState(ClerkController.ClerkState.Working);
            registrar.redirectionBonus = 0f;
        }
    }
}
// Assets/Scripts/Data/Actions/ProcessProjectDocumentExecutor.cs
using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Gameplay;
using Gameplay.Documents;
using Data.Documents;
using Enums;

public class ProcessProjectDocumentExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; 

    protected override IEnumerator ActionRoutine()
    {
        var clerk = staff as ClerkController;
        if (clerk == null || clerk.assignedWorkstation == null) 
        { 
            FinishAction(false); 
            yield break; 
        }

        yield return ReturnToWorkstation();
        if (!IsAtWorkstation) { FinishAction(false); yield break; }
        var stack = clerk.assignedWorkstation.documentStack;
        if (stack == null) 
        { 
            FinishAction(false); 
            yield break; 
        }

        // Ищем документ
        ProjectDocumentObject targetDoc = stack.FindPendingProjectDocument(clerk.currentRole);
        
        if (targetDoc == null) 
        { 
            FinishAction(false); 
            yield break; 
        }

        // Износ стола
        var durability = clerk.assignedWorkstation.GetComponent<OfficeObjectDurability>();
        if (durability != null && !durability.IsUsable())
        {
            clerk.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.service.desk_broken"), 3f, Color.red);
            FinishAction(false);
            yield break;
        }
        float efficiency = (durability != null) ? durability.GetEfficiencyMultiplier() : 1.0f;

        clerk.SetState(ClerkController.ClerkState.Working);
        clerk.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.project.internal_order"), 3f, Color.cyan);

        float workTime = Random.Range(4f, 6f) / efficiency;
        ShowProcessing(clerk.assignedWorkstation.transform.position, workTime);
        yield return new WaitForSeconds(workTime);

        if (targetDoc == null || !IsAtWorkstation || clerk.assignedWorkstation.documentStack != stack ||
            stack.FindPendingProjectDocument(clerk.currentRole) != targetDoc) { FinishAction(false); yield break; }
        var docData = targetDoc.documentData;

        if (clerk.clerkRole == ClerkController.ClerkRole.Registrar)
        {
            docData.processedByRegistrar = true;
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.UI_Stamp_Approve, transform.position);
        }
        else if (clerk.clerkRole == ClerkController.ClerkRole.Cashier || clerk.clerkRole == ClerkController.ClerkRole.Accountant)
        {
            int cost = 0;

            // ИСПРАВЛЕНИЕ: Используем глобальный Enum
            if (docData.docType == ProjectDocumentType.RegionUnlock && docData.targetRegion != null)
                cost = docData.targetRegion.unlockCostMoney;
            else if (docData.docType == ProjectDocumentType.JobPromotion && docData.targetJob != null)
                cost = docData.targetJob.costMoney;
            else if (docData.docType == ProjectDocumentType.FacilityUpgrade)
            {
                if (UpgradeManager.Instance != null)
                {
                    var upgrade = UpgradeManager.Instance.allUpgradesDatabase.FirstOrDefault(u => u.name == docData.targetUpgradeID);
                    if (upgrade != null) cost = upgrade.cost;
                }
            }

            if (PlayerWallet.Instance != null && PlayerWallet.Instance.GetCurrentMoney() >= cost)
            {
                PlayerWallet.Instance.AddMoney(-cost, UI.GameText.Get("ledger.project_payment", docData.documentName));
                docData.paidAtCashier = true;

                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.UI_Money_Income, transform.position);

                clerk.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.project.paid"), 2f, Color.green);
            }
            else
            {
                clerk.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.project.no_funds"), 3f, Color.red);
                FinishAction(false);
                yield break;
            }
        }

        targetDoc.UpdateVisuals();
        if (durability != null) durability.Degrade(2f);

        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
}
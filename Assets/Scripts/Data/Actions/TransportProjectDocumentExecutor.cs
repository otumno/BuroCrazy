// Assets/Scripts/Data/Actions/TransportProjectDocumentExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;
using Gameplay.Documents;
using Utilities;
using Characters;

public class TransportProjectDocumentExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Нельзя бросать важные документы на полпути!

    // Эти данные мы передадим при запуске действия вручную из контроллера
    public DocumentStack SourceStack { get; set; }
    public DocumentStack TargetStack { get; set; }
    public ProjectDocumentObject TargetDoc { get; set; }

    protected override IEnumerator ActionRoutine()
    {
        var manager = staff as OfficeManagerController;
        if (manager == null || SourceStack == null || TargetDoc == null)
        {
            FinishAction(false);
            yield break;
        }

        var board = TargetStack == null ? ScenePointsRegistry.Instance?.noticeBoard : null;
        var destination = TargetStack != null ? TargetStack.transform : board != null ? board.interactionPoint : null;
        if (destination == null) { FinishAction(false); yield break; }

        // 1. Идем к источнику
        manager.SetState(OfficeManagerController.ManagerState.MovingToDoc);
        yield return manager.MoveToTarget(SourceStack.transform.position, OfficeManagerController.ManagerState.MovingToDoc);

        // 2. Забираем документ
        // Проверяем, на месте ли он еще
        if (TargetDoc == null || !SourceStack.TakeSpecificDocument(TargetDoc))
        {
            manager.thoughtBubble?.ShowPriorityMessage("Где папка?!", 2f, Color.red);
            FinishAction(false);
            yield break;
        }

        bool delivered = false;
        RegisterCleanup(() =>
        {
            if (!delivered && TargetDoc != null && SourceStack != null)
                SourceStack.ReceiveExistingDocument(TargetDoc.gameObject, returningToSource: true);
        });

        // Визуализация в руках
        // Прикрепляем саму папку к руке (или используем StackHolder, если хотите упростить)
        var visuals = manager.GetComponent<CharacterVisuals>();
        Transform hand = visuals != null ? visuals.GetAttachPoint(CharacterVisuals.AttachPointType.Hand) : manager.transform;
        
        TargetDoc.transform.SetParent(hand);
        TargetDoc.transform.localPosition = Vector3.zero;
        TargetDoc.transform.localRotation = Quaternion.identity;

        manager.SetState(OfficeManagerController.ManagerState.CarryingDoc);
        manager.thoughtBubble?.ShowPriorityMessage("Доставка...", 2f, Color.white);

        // 3. Идем к цели
        yield return manager.MoveToTarget(destination.position, OfficeManagerController.ManagerState.CarryingDoc);
        if (board != null)
        {
            if (TargetDoc == null) { FinishAction(false); yield break; }
            board.PinPolicy(TargetDoc);
            delivered = true;
            FinishAction(true);
            yield break;
        }

        if (TargetDoc == null || TargetStack == null || !TargetStack.ReceiveExistingDocument(TargetDoc.gameObject))
        {
            FinishAction(false);
            yield break;
        }
        delivered = true;
        if (ArchiveManager.Instance != null && TargetStack == ArchiveManager.Instance.mainDocumentStack)
        {
            TargetDoc.documentData.archived = true;
            TargetDoc.UpdateVisuals();
            ProgressionManager.Instance?.ProcessCompletedDocument(TargetDoc.documentData);
        }

        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        
        manager.SetState(OfficeManagerController.ManagerState.Idle);
        FinishAction(true);
    }
}
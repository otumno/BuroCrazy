// Assets/Scripts/Data/Actions/TransportProjectDocumentExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;
using Gameplay.Documents;
using Utilities;
using Characters;
using System.Linq;

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

        var archive = ArchiveManager.Instance;
        bool toArchive = archive != null && archive.OwnsStack(TargetStack);
        Transform dropOff = toArchive ? archive.RequestDropOffPoint() : null;
        if (toArchive)
        {
            if (dropOff == null) { FinishAction(false); yield break; }
            RegisterCleanup(() => { if (archive != null) archive.FreeOverflowPoint(dropOff); });
            TargetStack = archive.GetDropOffStack(dropOff);
        }
        var board = TargetStack == null ? ScenePointsRegistry.Instance?.noticeBoard : null;
        var targetDesk = ScenePointsRegistry.Instance?.allServicePoints.FirstOrDefault(point => point != null && point.documentStack == TargetStack);
        var destination = toArchive ? dropOff : targetDesk != null && targetDesk.internCollectionPoint != null ? targetDesk.internCollectionPoint :
            TargetStack != null ? TargetStack.transform : board != null ? board.interactionPoint : null;
        if (destination == null) { FinishAction(false); yield break; }

        // 1. Идем к источнику
        manager.SetState(OfficeManagerController.ManagerState.MovingToDoc);
        var sourceDesk = ScenePointsRegistry.Instance?.allServicePoints.FirstOrDefault(point => point != null && point.documentStack == SourceStack);
        var pickup = sourceDesk != null && sourceDesk.internCollectionPoint != null ? sourceDesk.internCollectionPoint : SourceStack.transform;
        yield return manager.MoveToTarget(pickup.position, OfficeManagerController.ManagerState.MovingToDoc);

        // 2. Забираем документ
        // Проверяем, на месте ли он еще
        if (SourceStack == null || pickup == null || Vector2.Distance(manager.transform.position, pickup.position) > 1f ||
            TargetDoc == null || !SourceStack.TakeSpecificDocument(TargetDoc))
        {
            manager.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.delivery.folder_missing"), 2f, Color.red);
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
        manager.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.delivery.carrying"), 2f, Color.white);

        // 3. Идем к цели
        yield return manager.MoveToTarget(destination.position, OfficeManagerController.ManagerState.CarryingDoc);
        if (destination == null || (toArchive ? !ArchiveManager.IsWithinReach(manager.transform, destination.position)
            : Vector2.Distance(manager.transform.position, destination.position) > 1f))
        { FinishAction(false); yield break; }
        if (board != null)
        {
            if (TargetDoc == null) { FinishAction(false); yield break; }
            board.PinPolicy(TargetDoc);
            delivered = true;
            FinishAction(true);
            yield break;
        }

        if (TargetDoc == null || TargetStack == null ||
            (toArchive && (archive == null || archive.AvailableDropOffSlots(TargetStack) == 0)) ||
            !TargetStack.ReceiveExistingDocument(TargetDoc.gameObject))
        {
            FinishAction(false);
            yield break;
        }
        delivered = true;
        if (toArchive && TargetDoc.documentData != null)
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

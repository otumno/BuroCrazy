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
        if (manager == null || SourceStack == null || TargetStack == null || TargetDoc == null)
        {
            FinishAction(false);
            yield break;
        }

        // 1. Идем к источнику
        manager.SetState(OfficeManagerController.ManagerState.MovingToDoc);
        yield return staff.StartCoroutine(manager.MoveToTarget(SourceStack.transform.position, OfficeManagerController.ManagerState.MovingToDoc));

        // 2. Забираем документ
        // Проверяем, на месте ли он еще
        if (TargetDoc == null || !SourceStack.TakeSpecificDocument(TargetDoc))
        {
            manager.thoughtBubble?.ShowPriorityMessage("Где папка?!", 2f, Color.red);
            FinishAction(false);
            yield break;
        }

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
        yield return staff.StartCoroutine(manager.MoveToTarget(TargetStack.transform.position, OfficeManagerController.ManagerState.CarryingDoc));

        // 4. Кладем документ
        // Если это Архивация (финал)
        if (TargetStack == ArchiveManager.Instance.mainDocumentStack)
        {
            TargetDoc.documentData.archived = true;
            TargetDoc.UpdateVisuals();
            
            // СООБЩАЕМ СИСТЕМЕ ПРОГРЕССА
            ProgressionManager.Instance.ProcessCompletedDocument(TargetDoc.documentData);
            manager.thoughtBubble?.ShowPriorityMessage("Приказ исполнен!", 3f, Color.green);
        }

        // Физически кладем в стопку
        // Нам нужно передать ПРЕФАБ, но у нас уже есть ЖИВОЙ ОБЪЕКТ.
        // DocumentStack.AddProjectDocument требует префаб.
        // Поэтому мы уничтожаем объект в руке и создаем новый в стопке через AddProjectDocument
        // ИЛИ (лучше) доработаем DocumentStack, но пока используем трюк:
        
        // ТРЮК: Мы просто "вкладываем" объект в иерархию стопки и выравниваем его
        TargetDoc.transform.SetParent(TargetStack.transform);
        // Пересчитываем позицию (как в DocumentStack.AddDocumentInternal)
        Vector3 newPos = TargetStack.transform.position + new Vector3(0, TargetStack.CurrentSize * TargetStack.stackOffset, 0);
        TargetDoc.transform.position = newPos;
        TargetDoc.transform.rotation = TargetStack.transform.rotation * Quaternion.Euler(0, 0, Random.Range(-5f, 5f));
        
        // Регистрируем в списке стопки вручную (через Reflection или добавив метод, но пока просто AddDocumentToStack() добавит "пустышку", а нам нужен наш объект)
        // Чтобы не ломать инкапсуляцию, используем метод AddProjectDocument, передавая данные, а объект в руке уничтожаем.
        // Это самый безопасный способ.
        
        // Сохраняем ссылку на префаб (если он был сохранен) или используем дефолтный красный
        // Для простоты, допустим, у нас есть универсальный префаб красной папки в менеджере
        // Или просто перенесем Transform, но DocumentStack.visualStack (private list) не обновится.
        
        // РЕШЕНИЕ: Добавляем документ "через парадную дверь"
        // Нам нужно получить префаб. Возьмем копию самого объекта.
        bool added = TargetStack.AddProjectDocument(TargetDoc.documentData, TargetDoc.gameObject); // Передаем сам объект как "префаб" (Instantiate сработает как клон)
        if (added)
        {
            Destroy(TargetDoc.gameObject); // Уничтожаем копию в руке
        }
        else
        {
            // Стопка полна? Кладем рядом или уничтожаем (документ потерян)
            Debug.LogError("Целевая стопка полна! Документ потерян.");
            Destroy(TargetDoc.gameObject);
        }

        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        
        manager.SetState(OfficeManagerController.ManagerState.Idle);
        FinishAction(true);
    }
}
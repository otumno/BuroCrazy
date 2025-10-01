using UnityEngine;
using System.Collections;

public class DefaultActionExecutor : ActionExecutor
{
    protected override IEnumerator ActionRoutine()
    {
        // --- САМЫЙ ВАЖНЫЙ ШАГ: СБРОС ВЫГОРАНИЯ ---
        staff.SetCurrentFrustration(0f);
        Debug.Log($"<color=cyan>[ДЕЙСТВИЕ ПО УМОЛЧАНИЮ]</color> Выгорание для {staff.characterName} сброшено до 0.");

        // Определяем, что делать в зависимости от роли
        if (staff is GuardMovement guard)
        {
            // --- Логика для Охранника ---
            guard.SetState(GuardMovement.GuardState.OnPost);
            Transform post = ScenePointsRegistry.Instance?.guardPostPoint;
            if (post != null)
            {
                // Идем на пост
                yield return staff.StartCoroutine(guard.MoveToTarget(post.position, GuardMovement.GuardState.OnPost));
                // "Отдыхаем" на посту 15 секунд
                yield return new WaitForSeconds(15f);
            }
        }
        else if (staff is ServiceWorkerController worker)
        {
            // --- Логика для Уборщика ---
            worker.SetState(ServiceWorkerController.WorkerState.Idle);
            RectZone homeZone = ScenePointsRegistry.Instance?.staffHomeZone;
            if (homeZone != null)
            {
                // Идем в "подсобку" (домашнюю зону)
                yield return staff.StartCoroutine(worker.MoveToTarget(homeZone.GetRandomPointInside(), ServiceWorkerController.WorkerState.Idle));
                // Отдыхаем там 15 секунд
                yield return new WaitForSeconds(15f);
            }
        }
        else if (staff is ClerkController clerk)
        {
            // --- Логика для Клерка ---
            clerk.SetState(ClerkController.ClerkState.OnBreak);
            Transform breakPoint = ScenePointsRegistry.Instance?.RequestKitchenPoint();
            if (breakPoint != null)
            {
                // Идем на кухню
                yield return staff.StartCoroutine(clerk.MoveToTarget(breakPoint.position, ClerkController.ClerkState.OnBreak));
                // Пьем чай 15 секунд
                yield return new WaitForSeconds(15f);
                ScenePointsRegistry.Instance.FreeKitchenPoint(breakPoint); // Освобождаем место
            }
        }
        else
        {
            // --- Логика для всех остальных (например, Стажер) ---
            // Просто ждем на месте 10 секунд
            yield return new WaitForSeconds(10f);
        }

        // Сообщаем "мозгу", что действие по умолчанию завершено
        FinishAction();
    }
}
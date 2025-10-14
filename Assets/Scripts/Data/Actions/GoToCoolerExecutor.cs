// Файл: Assets/Scripts/Data/Actions/GoToCoolerExecutor.cs
using UnityEngine;
using System.Collections;

public class GoToCoolerExecutor : ActionExecutor
{
    public override bool IsInterruptible => true; // Разговор у кулера можно прервать

    protected override IEnumerator ActionRoutine()
    {
        // Предполагаем, что у вас будет точка для кулера в ScenePointsRegistry
        // Если нет, можно использовать любую другую точку отдыха, например, кухню
        Transform coolerPoint = ScenePointsRegistry.Instance?.RequestKitchenPoint();
        if (coolerPoint == null)
        {
            FinishAction();
            yield break;
        }

        staff.thoughtBubble?.ShowPriorityMessage("Пойду поболтаю...", 2f, Color.gray);
        
        if (staff is ClerkController clerk)
        {
            clerk.SetState(ClerkController.ClerkState.GoingToBreak); // Используем общее состояние
            yield return staff.StartCoroutine(staff.MoveToTarget(coolerPoint.position, ClerkController.ClerkState.OnBreak.ToString()));
        }
        // ... здесь можно добавить логику для других типов сотрудников ...

        yield return new WaitForSeconds(Random.Range(10f, 20f));

        // Эффекты: повышает мораль, но и потребность сходить в туалет
        staff.morale = 1f;
        staff.bladder = Mathf.Clamp01(staff.bladder + 0.3f); // +30% к bladder
        
        ScenePointsRegistry.Instance.FreeKitchenPoint(coolerPoint);
        Debug.Log($"[AI Needs] {staff.name} пообщался у кулера. Мораль восстановлена.");
        FinishAction();
    }
}
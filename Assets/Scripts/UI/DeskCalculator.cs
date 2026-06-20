using UnityEngine;
using Managers;
using System.Linq;

public class DeskCalculator : DeskInteractiveItem
{
    // Переопределяем проверку доступности
    public override void CheckAvailability()
    {
        base.CheckAvailability();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // --- ДЕБАГ ПРОВЕРКА ---
        if (PlayerInputController.DebugForceBookkeeping)
        {
            isInteractable = true;
            var r = GetComponent<SpriteRenderer>();
            if (r != null) r.color = Color.white;
            return;
        }
        // ----------------------
#endif

        if (HiringManager.Instance == null)
        {
            isInteractable = false;
            return;
        }

        // Проверяем, есть ли у нас хоть один Бухгалтер
        // Логика взята из BookkeepingButtonController
        bool hasBookkeeper = HiringManager.Instance.AllStaff.Any(s =>
            s.activeActions.Any(a => a.actionType == ActionType.DoBookkeeping)
        );

        isInteractable = hasBookkeeper;

        // Опционально: можно менять цвет спрайта, если он недоступен
        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = isInteractable ? Color.white : new Color(0.5f, 0.5f, 0.5f); // Серый, если нет бухгалтера
        }
    }

    private void OnEnable()
    {
        // Проверяем каждый раз при включении стола
        CheckAvailability();
    }
}
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using Managers;
using Scriptables.Audio;
using Characters;

[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))] // Гарантируем коллайдер
public class WorkstationClickTrigger : MonoBehaviour
{
    public enum TriggerType { Workstation, DirectorDesk, SecurityDoor }

    [Header("Тип триггера")]
    [Tooltip("Workstation - рабочее место, DirectorDesk - стол директора, SecurityDoor - турникет")]
    public TriggerType type = TriggerType.Workstation;

    [Header("Цель (только для Workstation)")]
    [Tooltip("ServicePoint, на который отправляется Директор по клику")]
    public ServicePoint targetWorkstation;

    [Header("Звуки")]
    public SoundID soundIdOnClick = SoundID.UI_Click_Default;
    public SoundID soundIdOnHover = SoundID.UI_Hover; // <--- ИЗМЕНЕНО: теперь звук по умолчанию назначен

    [Header("Отладка (Не трогать)")]
    [Tooltip("Сюда скрипт сам подтянет SpriteRenderer дочки 'highlight'")]
    [SerializeField] private SpriteRenderer _highlightRenderer;

    // === РЕПЛИКИ В ПУТИ ===
    private static readonly string[] registrarMoveReplics = {
        "Ну что ж, поработаю регистратором, если надо.",
        "Встречать гостей — мое призвание.",
        "Покажу им, как надо работать с людьми."
    };
    private static readonly string[] office1MoveReplics = {
        "Бумажная работа... Опять.",
        "Форма 1 сама себя не заполнит.",
        "Пора ставить печати."
    };
    private static readonly string[] office2MoveReplics = {
        "Бланк номер два, поехали.",
        "Больше бюрократии богу бюрократии!",
        "Никто не проштампует это лучше меня."
    };
    private static readonly string[] archiveMoveReplics = {
        "Опять дышать пылью...",
        "Пора навести порядок в этих бумажках.",
        "Архивы ждут."
    };
    private static readonly string[] cashierMoveReplics = {
        "Пойду считать чужие деньги.",
        "Звон монет успокаивает нервы.",
        "Касса свободна, подходите!"
    };
    private static readonly string[] accountantMoveReplics = {
        "Пора проверить цифры.",
        "Документы сами себя не посчитают.",
        "Иду к своему столу."
    };

    // === РЕПЛИКИ НА РАБОЧЕМ МЕСТЕ ===
    private static readonly string[] registrarArrivedReplics = {
        "Приступил к обязанностям регистратора.",
        "Следующий, подходите!",
        "Добро пожаловать в Бюро. Чем могу помочь?"
    };
    private static readonly string[] office1ArrivedReplics = {
        "Так, где моя печать?",
        "Стол номер один открыт.",
        "Давайте ваши первые формы."
    };
    private static readonly string[] office2ArrivedReplics = {
        "Стол номер два работает.",
        "Приступил к сложной бюрократии.",
        "Готов к обработке формы номер два."
    };
    private static readonly string[] archiveArrivedReplics = {
        "Открываю архивы.",
        "Запах старой бумаги... приступим.",
        "Я готов искать ваши дела."
    };
    private static readonly string[] cashierArrivedReplics = {
        "Касса открыта.",
        "Готовьте ваши денежки.",
        "Приступил к финансовым операциям."
    };
    private static readonly string[] accountantArrivedReplics = {
        "Начинаю оптимизацию.",
        "Конверты ждут.",
        "Приступил к бухгалтерии."
    };

    private void Awake()
    {
        // Ищем дочерний объект с именем "highlight"
        foreach (Transform child in transform)
        {
            if (child.name.Equals("highlight", System.StringComparison.OrdinalIgnoreCase))
            {
                _highlightRenderer = child.GetComponent<SpriteRenderer>();
                break;
            }
        }

        if (_highlightRenderer != null)
        {
            // Всегда выключаем при старте игры, даже если в редакторе он был включен
            _highlightRenderer.enabled = false;
        }
        else
        {
            Debug.LogWarning($"[WorkstationClickTrigger] На объекте {gameObject.name} не найдена дочка с именем 'highlight'!");
        }
    }

    private void OnMouseEnter()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (!CanInteract()) return;

        if (soundIdOnHover != SoundID.None && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound(soundIdOnHover, transform.position);
        }

        if (_highlightRenderer != null)
        {
            _highlightRenderer.enabled = true;
        }
    }

    private void OnMouseExit()
    {
        if (_highlightRenderer != null)
        {
            _highlightRenderer.enabled = false;
        }
    }

    private void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (!CanInteract())
        {
            if (type == TriggerType.Workstation)
            {
                var staff = targetWorkstation?.GetAssignedStaff();
                if (staff != null && staff != DirectorAvatarController.Instance)
                {
                    DirectorAvatarController.Instance?.thoughtBubble?.ShowPriorityMessage("Это место занято!", 2f, Color.red);
                }
            }
            return;
        }

        StartCoroutine(ExecuteClickWithDelay());
    }

    private IEnumerator ExecuteClickWithDelay()
    {
        // Ждем 1 кадр для решения конфликта с PlayerInputController
        yield return null;

        if (soundIdOnClick != SoundID.None && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound(soundIdOnClick, transform.position);
        }

        var dir = DirectorAvatarController.Instance;
        if (_highlightRenderer != null) _highlightRenderer.enabled = false;

        switch (type)
        {
            case TriggerType.Workstation:
                if (IsDirectorWorkingHere())
                {
                    dir.StopManualWork();
                }
                else
                {
                    dir.StartWorkingAt(targetWorkstation);
                    ShowRandomReplic(false); // В пути
                    StartCoroutine(WaitForArrivalAndSpeak());
                }
                break;

            case TriggerType.DirectorDesk:
                dir.GoToDesk();
                dir.thoughtBubble?.ShowPriorityMessage("Пора заняться своими делами.", 3f, Color.white);
                break;

            case TriggerType.SecurityDoor:
                dir.GoAndOperateBarrier();
                bool isActive = GuardManager.Instance?.securityBarrier?.IsActive() ?? false;
                string barrierMsg = isActive ? "Открываю офис." : "Закрываю офис.";
                dir.thoughtBubble?.ShowPriorityMessage(barrierMsg, 3f, Color.white);
                break;
        }
    }

    private IEnumerator WaitForArrivalAndSpeak()
    {
        var dir = DirectorAvatarController.Instance;
        if (dir == null) yield break;

        yield return new WaitUntil(() => 
            dir.GetCurrentState() == DirectorAvatarController.DirectorState.WorkingAtStation || 
            dir.GetCurrentState() == DirectorAvatarController.DirectorState.Idle);

        if (dir.GetCurrentState() == DirectorAvatarController.DirectorState.WorkingAtStation && dir.GetWorkstation() == targetWorkstation)
        {
            ShowRandomReplic(true); // Прибыл
        }
    }

    private bool CanInteract()
    {
        if (DirectorAvatarController.Instance == null) return false;
        if (DirectorAvatarController.Instance.IsInUninterruptibleAction) return false;
        
        // Блокируем взаимодействие во время катсцен (включая туториал)
        var inputController = DirectorAvatarController.Instance.GetComponent<PlayerInputController>();
        if (inputController != null && inputController.IsCutscenePlaying) return false;

        // Проверка занятости стола только для Workstation
        if (type == TriggerType.Workstation && targetWorkstation != null)
        {
            var currentStaff = targetWorkstation.GetAssignedStaff();
            if (currentStaff != null && currentStaff != DirectorAvatarController.Instance) return false;
        }

        return true;
    }

    private bool IsDirectorWorkingHere()
    {
        return DirectorAvatarController.Instance != null && 
               DirectorAvatarController.Instance.GetCurrentState() == DirectorAvatarController.DirectorState.WorkingAtStation && 
               DirectorAvatarController.Instance.GetWorkstation() == targetWorkstation;
    }

    private void ShowRandomReplic(bool isArrival)
    {
        string[] replics = GetReplicsForDeskId(targetWorkstation.deskId, isArrival);
        if (replics == null || replics.Length == 0) return;

        string randomReplic = replics[Random.Range(0, replics.Length)];
        DirectorAvatarController.Instance.thoughtBubble?.ShowPriorityMessage(randomReplic, 3.5f, Color.white);
    }

    private string[] GetReplicsForDeskId(int deskId, bool isArrival)
    {
        if (!isArrival)
        {
            return deskId switch {
                0 => registrarMoveReplics,
                1 => office1MoveReplics,
                2 => office2MoveReplics,
                3 => archiveMoveReplics,
                -1 => cashierMoveReplics,
                4 => accountantMoveReplics,
                _ => new string[] { "Иду работать!" }
            };
        }
        else
        {
            return deskId switch {
                0 => registrarArrivedReplics,
                1 => office1ArrivedReplics,
                2 => office2ArrivedReplics,
                3 => archiveArrivedReplics,
                -1 => cashierArrivedReplics,
                4 => accountantArrivedReplics,
                _ => new string[] { "Приступил к работе!" }
            };
        }
    }
}

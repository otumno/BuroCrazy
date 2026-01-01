using BuroDebug;
using UnityEngine;
using Managers;
using DialogueSystem.Data;

public class MainMenuDialogueTrigger : MonoBehaviour
{
    [Header("Диалог")]
    public DialogueGraph introDialogue;
    public string saveKey = "HAS_SEEN_INTRO";

    [Header("Система, которую надо задержать")]
    [Tooltip("Ссылка на объект HelpSystem или TutorialManager")]
    public GameObject helpSystemObject; 

    private bool _hasSeen;

    private void Awake()
    {
        // 1. Проверяем состояние СРАЗУ при загрузке объекта
        _hasSeen = PlayerPrefs.GetInt(saveKey, 0) == 1;

        if (DebugSettings.IsDebug)
            _hasSeen = false;

        // 2. Если мы еще НЕ видели интро — жестко гасим маскота, 
        // чтобы он даже не успел пикнуть.
        if (!_hasSeen && helpSystemObject != null)
        {
            helpSystemObject.SetActive(false);
        }
    }

    private void Start()
    {
        // 3. Логика запуска
        if (!_hasSeen)
        {
            // Ждем чуть-чуть, чтобы сцена прогрузилась, и запускаем диалог
            Invoke(nameof(StartIntro), 0.5f);
        }
        else
        {
            // ОТВЕТ НА ВОПРОС 2:
            // Если интро уже видели — убеждаемся, что маскот включен
            if (helpSystemObject != null && !helpSystemObject.activeSelf) 
            {
                helpSystemObject.SetActive(true);
            }
        }
    }

    private void StartIntro()
    {
        if (introDialogue != null && DialogueUIManager.Instance != null)
        {
            // Запускаем диалог с Callback-ом
            DialogueUIManager.Instance.StartDialogue(introDialogue, null, () => 
            {
                Debug.Log("Интро завершено. Активируем помощника.");
                
                // ОТВЕТ НА ВОПРОС 1:
                // Включаем маскота обратно. Он "проснется" и начнет работать.
                if (helpSystemObject != null) 
                {
                    helpSystemObject.SetActive(true);
					
					var mascot = helpSystemObject.GetComponentInChildren<Utilities.TutorialMascot>();
                    if (mascot != null)
                    {
                        mascot.ForceStartSequence();
                    }
					
                }
                
                PlayerPrefs.SetInt(saveKey, 1);
                PlayerPrefs.Save();
            });
        }
        else
        {
            // Если что-то пошло не так (нет менеджера), на всякий случай включаем маскота,
            // чтобы игрок не остался без UI.
            if (helpSystemObject != null) helpSystemObject.SetActive(true);
        }
    }

    [ContextMenu("Reset Intro Flag")]
    public void ResetIntroFlag()
    {
        PlayerPrefs.DeleteKey(saveKey);
        Debug.Log("Интро сброшено! Перезапустите сцену.");
    }
}
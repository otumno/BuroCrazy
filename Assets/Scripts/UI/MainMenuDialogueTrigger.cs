using BuroDebug;
using UnityEngine;
using Managers;
using DialogueSystem.Data;

public class MainMenuDialogueTrigger : MonoBehaviour
{
    [Header("Диалог")]
    public DialogueGraph introDialogue;
    public string saveKey = "HAS_SEEN_INTRO";

    [Header("Настройки")]
    [Tooltip("Включить — показывать приветствие каждый раз при запуске")]
    public bool alwaysShowIntro = false;

    [Header("Система, которую надо задержать")]
    [Tooltip("Ссылка на объект HelpSystem или TutorialManager")]
    public GameObject helpSystemObject; 

    private bool _hasSeen;

    private void Awake()
    {
        _hasSeen = PlayerPrefs.HasKey(saveKey) && !alwaysShowIntro;

        if (!_hasSeen && helpSystemObject != null)
        {
            helpSystemObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (!_hasSeen)
        {
            Invoke(nameof(StartIntro), 0.5f);
        }
        else
        {
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
            DialogueUIManager.Instance.StartDialogue(introDialogue, null, () => 
            {
                Debug.Log("Интро завершено. Активируем помощника.");
                
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

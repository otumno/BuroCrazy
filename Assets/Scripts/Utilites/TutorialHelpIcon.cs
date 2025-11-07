// Файл: Assets/Scripts/UI/Tutorial/TutorialHelpIcon.cs (ФИНАЛЬНАЯ ВЕРСИЯ)
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button), typeof(AudioSource))]
public class TutorialHelpIcon : MonoBehaviour
{
    [Header("Звуки")]
    [Tooltip("Звук, который проигрывается при нажатии на эту иконку")]
    [SerializeField] private AudioClip clickSound;
    
    [Tooltip("Звук, который проигрывается при сбросе прогресса")]
    [SerializeField] private AudioClip resetSound;

    private Button helpButton;
    private AudioSource audioSource;

    void Awake()
    {
        helpButton = GetComponent<Button>();
        helpButton.onClick.AddListener(OnHelpIconClicked);
        
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.ignoreListenerPause = true; 
    }

    private void OnHelpIconClicked()
    {
        if (TutorialMascot.Instance == null)
        {
            Debug.LogError("[TutorialHelpIcon] Не могу найти TutorialMascot.Instance!");
            return;
        }

        if (TutorialMascot.Instance.AreAllSpotsOnScreenVisited())
        {
            // Все подсказки уже были показаны. Сбрасываем прогресс.
            PlaySound(resetSound);
            TutorialMascot.Instance.ResetCurrentScreenTutorial();
        }
        else
        {
            // Еще есть непоказанные подсказки. Показываем следующую.
            PlaySound(clickSound); // Исправлена опечатка (был 'mascotClickSound')
            TutorialMascot.Instance.ShowNextUnvisitedSpotOnScreen();
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
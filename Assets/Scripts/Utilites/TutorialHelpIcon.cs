// Файл: Assets/Scripts/UI/Tutorial/TutorialHelpIcon.cs
// ВЕРСИЯ С ЛОГАМИ И ИСПРАВЛЕНИЯМИ GDD

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
        // --- ИСПРАВЛЕНИЕ 7 (Логика '?' из ТЗ) ---
        Debug.Log("<color=cyan>[TutorialHelpIcon] Кнопка '?' нажата.</color>");

        if (TutorialMascot.Instance == null)
        {
            Debug.LogError("[TutorialHelpIcon] Не могу найти TutorialMascot.Instance!");
            return;
        }

        // GDD 4.3: "Если Папочка уже занята ... нажатие на '?' игнорируется."
        if (TutorialMascot.Instance.IsBusy())
        {
            Debug.LogWarning("[TutorialHelpIcon] Клик по '?' проигнорирован: Маскот занят (IsBusy() == true).");
            PlaySound(clickSound);
            return;
        }
        
        // --- НАЧАЛО ИСПРАВЛЕНИЯ (Логика из вашего ТЗ) ---
        // "вызвать обратно можно по нажатии на icon маскота ... сбрасываются и будут показаны заново"
        
        Debug.Log("[TutorialHelpIcon] Вызов ResetCurrentScreenTutorial() по требованию пользователя.");
        PlaySound(resetSound); // Всегда проигрываем звук сброса
        TutorialMascot.Instance.ResetCurrentScreenTutorial();
        // --- КОНЕЦ ИСПРАВЛЕНИЯ ---
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
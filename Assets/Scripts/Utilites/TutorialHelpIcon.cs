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
        // --- НОВЫЙ ЛОГ ---
        Debug.Log("<color=cyan>[TutorialHelpIcon] Кнопка '?' нажата.</color>");

        if (TutorialMascot.Instance == null)
        {
            Debug.LogError("[TutorialHelpIcon] Не могу найти TutorialMascot.Instance!");
            return;
        }

        // --- ИСПРАВЛЕНИЕ (GDD 4.3: Защита от спама) ---
        // GDD: "Если Папочка уже занята (анимируется, показывает подсказку), нажатие на '?' игнорируется."
        if (TutorialMascot.Instance.IsBusy())
        {
            Debug.LogWarning("[TutorialHelpIcon] Клик по '?' проигнорирован: Маскот занят (IsBusy() == true).");
            PlaySound(clickSound);
            return;
        }
        // --- КОНЕЦ ИСПРАВЛЕНИЯ ---


        // GDD 4.3: Логика "Сброса" или "Следующей подсказки"
        if (TutorialMascot.Instance.AreAllSpotsInCurrentContextVisited())
        {
            // GDD 4.3: "(ВСЕ подсказки... просмотрены): Нажатие сбрасывает прогресс."
            Debug.Log("[TutorialHelpIcon] Все подсказки в этом контексте просмотрены. Вызов ResetCurrentScreenTutorial().");
            PlaySound(resetSound);
            TutorialMascot.Instance.ResetCurrentScreenTutorial();
        }
        else
        {
            // GDD 4.3: "(есть непросмотренные подсказки...): Нажатие немедленно вызывает следующую..."
            Debug.Log("[TutorialHelpIcon] Есть непросмотренные подсказки. Вызов RequestNextHintSmart().");
            PlaySound(clickSound);
            
            // --- ИСПРАВЛЕНИЕ: Вызываем публичный void метод, а не корутину ---
            TutorialMascot.Instance.RequestNextHintSmart();
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
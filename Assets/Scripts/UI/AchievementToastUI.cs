// Файл: Assets/Scripts/UI/AchievementToastUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic; // <<< ИСПРАВЛЕНИЕ 1: Добавлена эта строка

[RequireComponent(typeof(CanvasGroup))]
public class AchievementToastUI : MonoBehaviour
{
    [Header("Ссылки на UI")]
    [SerializeField] private Image achievementIcon;
    [SerializeField] private TextMeshProUGUI achievementNameText;

    [Header("Анимация")]
    [SerializeField] private float fadeInTime = 0.3f;
    [SerializeField] private float displayTime = 3.0f;
    [SerializeField] private float fadeOutTime = 0.5f;

    [Header("Звук")]
    [SerializeField] private AudioClip unlockSound;
    private AudioSource audioSource;

    private CanvasGroup canvasGroup;
    private Coroutine activeCoroutine;
    
    // Теперь эта строка будет работать 
    private Queue<AchievementData> achievementQueue = new Queue<AchievementData>();

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        
        canvasGroup.alpha = 0f;
    }

    void OnEnable()
    {
        // Подписываемся на событие менеджера
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnAchievementUnlocked += OnUnlockReceived;
        }
    }

    void OnDisable()
    {
        // Отписываемся
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnAchievementUnlocked -= OnUnlockReceived;
        }
    }

    private void OnUnlockReceived(AchievementData data)
    {
        // Добавляем ачивку в очередь (на случай, если игрок получит 2 сразу)
        achievementQueue.Enqueue(data);
        
        // Если уже не показывается другая ачивка, запускаем показ
        if (activeCoroutine == null)
        {
            activeCoroutine = StartCoroutine(ShowToastSequence());
        }
    }

    private IEnumerator ShowToastSequence()
    {
        while (achievementQueue.Count > 0)
        {
            AchievementData data = achievementQueue.Dequeue();

            // 1. Настраиваем
            
            // --- <<< ИСПРАВЛЕНИЕ 2: Изменен формат текста >>> ---
            achievementNameText.text = $"Открыт доступ к архивной записи \"{data.displayName}\"";
            // --- <<< КОНЕЦ ИСПРАВЛЕНИЯ 2 >>> ---
            
            achievementIcon.sprite = data.iconUnlocked; // Показываем цветную иконку
            if (unlockSound != null) audioSource.PlayOneShot(unlockSound);

            // 2. Плавное появление (Fade In)
            float timer = 0f;
            while (timer < fadeInTime)
            {
                timer += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeInTime);
                yield return null;
            }
            canvasGroup.alpha = 1f;

            // 3. Показываем (Dwell)
            yield return new WaitForSecondsRealtime(displayTime);

            // 4. Плавное исчезание (Fade Out)
            timer = 0f;
            while (timer < fadeOutTime)
            {
                timer += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeOutTime);
                yield return null;
            }
            canvasGroup.alpha = 0f;
        }

        // Очередь пуста, сбрасываем корутину
        activeCoroutine = null;
    }
}
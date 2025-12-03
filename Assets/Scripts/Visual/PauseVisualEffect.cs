using UnityEngine;
using UnityEngine.Rendering; // Нужен для Volume

public class PauseVisualEffect : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Ссылка на Volume, который мы крутим")]
    public Volume pauseVolume;
    
    [Tooltip("Скорость перехода (чем больше, тем быстрее)")]
    public float transitionSpeed = 5f;

    void Start()
    {
        // Если забыли назначить, пробуем найти на этом же объекте
        if (pauseVolume == null) 
            pauseVolume = GetComponent<Volume>();
		pauseVolume.weight = 0.001f;
    }

    void Update()
    {
        if (pauseVolume == null) return;

        // Проверяем: игра на паузе? (Время стоит?)
        // Используем Time.timeScale, так как MainUIManager управляет именно им.
        bool isPaused = Time.timeScale == 0f;

        // Если пауза -> хотим Вес = 1 (черно-белое).
        // Если играем -> хотим Вес = 0 (цветное).
        float targetWeight = isPaused ? 1f : 0f;

        // Плавно меняем вес
        // ВАЖНО: Используем unscaledDeltaTime, иначе на паузе анимация застынет!
        pauseVolume.weight = Mathf.Lerp(pauseVolume.weight, targetWeight, Time.unscaledDeltaTime * transitionSpeed);
    }
}
using UnityEngine;
using UnityEngine.Rendering; // Нужен для Volume

public class PauseVisualEffect : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Ссылка на Volume, который мы крутим")]
    public Volume pauseVolume;
    
    [Tooltip("Скорость перехода (чем больше, тем быстрее)")]
    public float transitionSpeed = 5f;

    private void Start()
    {
        pauseVolume ??= GetComponent<Volume>();
		pauseVolume.weight = 0.001f; // поч не 0?)
    }

    private void Update()
    {
        if (pauseVolume == null)
            return;

        // по-хорошему, управление паузой должно где-то лежать в 1 скрипте, все остальные должны только к нему обращаться
        // так потом найти проще, если где-то будет баг, что пауза не снялась или не ставится,
        // или снимается когда ui какой-нибудь паузящий активен ещё
        bool isPaused = Time.timeScale == 0f;

        // Если пауза -> хотим Вес = 1 (черно-белое).
        // Если играем -> хотим Вес = 0 (цветное).
        float targetWeight = isPaused ? 1f : 0f;

        // Плавно меняем вес
        // ВАЖНО: Используем unscaledDeltaTime, иначе на паузе анимация застынет!
        pauseVolume.weight = Mathf.Lerp(pauseVolume.weight, targetWeight, Time.unscaledDeltaTime * transitionSpeed);
    }
}
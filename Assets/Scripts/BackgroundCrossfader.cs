using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BackgroundCrossfader : MonoBehaviour
{
    [Header("Фоны для смены")]
    [Tooltip("Картинка, которая видна изначально")]
    [SerializeField] private Image initialBackground;
    [Tooltip("Картинка, которая плавно появится")]
    [SerializeField] private Image finalBackground;

    [Header("Настройки анимации")]
    [Tooltip("Задержка в секундах перед началом смены фона")]
    [SerializeField] private float fadeDelay = 0.5f;
    [Tooltip("Длительность смены фона")]
    [SerializeField] private float fadeDuration = 2.0f;

    // Этот метод вызывается автоматически, когда панель становится активной
    void OnEnable()
    {
        StartCoroutine(CrossfadeRoutine());
    }

    private IEnumerator CrossfadeRoutine()
    {
        // Устанавливаем начальное состояние: старый фон виден, новый - нет
        initialBackground.canvasRenderer.SetAlpha(1.0f);
        finalBackground.canvasRenderer.SetAlpha(0.0f);
        finalBackground.gameObject.SetActive(true);

        // Ждем небольшую задержку
        yield return new WaitForSeconds(fadeDelay);

        // Запускаем плавное затухание старого фона
        initialBackground.CrossFadeAlpha(0.0f, fadeDuration, true);
        
        // Одновременно запускаем плавное появление нового фона
        finalBackground.CrossFadeAlpha(1.0f, fadeDuration, true);

        // Ждем окончания анимации и выключаем старый фон
        yield return new WaitForSeconds(fadeDuration);
        initialBackground.gameObject.SetActive(false);
    }
}
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Необходимо для перемешивания

public class SlideshowFader : MonoBehaviour
{
    [Header("Настройки изображений")]
    [Tooltip("Список изображений для показа в слайд-шоу.")]
    public List<Sprite> images = new List<Sprite>();

    [Header("Ссылки на компоненты UI")]
    [SerializeField] private Image imageA;
    [SerializeField] private Image imageB;

    [Header("Настройки времени")]
    public float fadeDuration = 2.0f;
    public float displayDuration = 5.0f;

    // Списки для управления порядком изображений
    private List<int> imageIndices = new List<int>();
    private int listPosition = 0;

    void OnEnable()
    {
        if (images.Count < 2 || imageA == null || imageB == null)
        {
            Debug.LogError("[SlideshowFader] Для работы нужно как минимум 2 изображения и ссылки на оба компонента Image!", this);
            enabled = false; // Отключаем скрипт, если он настроен неверно
            return;
        }

        SetupAndStartSlideshow();
    }

    private void SetupAndStartSlideshow()
    {
        // --- Логика случайного порядка ---
        // 1. Создаем список индексов для всех изображений, кроме первого (индекс 0)
        imageIndices = Enumerable.Range(1, images.Count - 1).ToList();
        
        // 2. Перемешиваем этот список
        System.Random rng = new System.Random();
        imageIndices = imageIndices.OrderBy(a => rng.Next()).ToList();
        
        // 3. Вставляем индекс 0 в самое начало, чтобы всегда начинать с первой картинки
        imageIndices.Insert(0, 0);
        listPosition = 0;

        // --- Начальная настройка изображений ---
        imageA.sprite = images[imageIndices[0]];
        imageA.canvasRenderer.SetAlpha(1f);
        imageB.canvasRenderer.SetAlpha(0f);
        
        // Запускаем корутину
        StartCoroutine(SlideshowRoutine());
    }

    private IEnumerator SlideshowRoutine()
    {
        Image currentImage = imageA;
        Image nextImage = imageB;

        while (true)
        {
            // Ждем, пока текущий слайд на экране
            yield return new WaitForSeconds(displayDuration);

            // Готовим следующий слайд
            listPosition++;
            // Если дошли до конца списка, перемешиваем его заново и начинаем с начала
            if (listPosition >= imageIndices.Count)
            {
                listPosition = 0;
                // Можно добавить повторное перемешивание здесь, если хотите новый порядок каждый цикл
            }
            
            nextImage.sprite = images[imageIndices[listPosition]];

            // --- ОБНОВЛЕННАЯ ЛОГИКА ПЕРЕХОДА ---
            // Убеждаемся, что следующее изображение будет рисоваться поверх текущего
            nextImage.transform.SetAsLastSibling();

            // Плавно проявляем следующее изображение
            float elapsedTime = 0f;
            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsedTime / fadeDuration);
                nextImage.canvasRenderer.SetAlpha(alpha);
                yield return null;
            }
            nextImage.canvasRenderer.SetAlpha(1f);

            // Прячем старое изображение (которое теперь находится снизу)
            currentImage.canvasRenderer.SetAlpha(0f);

            // Меняем роли для следующей итерации
            Image temp = currentImage;
            currentImage = nextImage;
            nextImage = temp;
        }
    }
}
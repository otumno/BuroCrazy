using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Data.Calendar;
using Scriptables;
using UnityEngine;
using UnityEngine.Rendering.Universal; // Обязательно для Light2D

namespace Managers
{
    public class LightManager : MonoBehaviour
    {
        public static LightManager Instance { get; private set; }

        [Header("Глобальное освещение")]
        [Tooltip("Перетащите сюда Global Light 2D со сцены")]
        public Light2D globalLight;
        public float lightFadeDuration = 0.5f;

        [Header("Локальные источники света")]
        [Tooltip("Список всех ламп на сцене, которые должны включаться/выключаться по расписанию")]
        public List<GameObject> allControllableLights;

        private Coroutine lightManagementCoroutine;

        void Awake()
        {
            if (Instance != null && Instance != this) 
            { 
                Destroy(gameObject); 
            } 
            else 
            { 
                Instance = this; 
            }
        }

        void Start()
        {
            // Подписываемся на события DayPeriodManager
            if (DayPeriodManager.Instance != null)
            {
                DayPeriodManager.Instance.OnPeriodChanged += HandlePeriodChange;
            }
            else
            {
                Debug.LogError("[LightManager] DayPeriodManager не найден! Свет не будет переключаться.");
            }
        }

        void OnDestroy()
        {
            if (DayPeriodManager.Instance != null)
            {
                DayPeriodManager.Instance.OnPeriodChanged -= HandlePeriodChange;
            }
        }

        void Update()
        {
            // Плавное изменение цвета глобального света в течение периода
            UpdateGlobalLighting();
        }

        private void UpdateGlobalLighting()
        {
            if (globalLight == null || DayPeriodManager.Instance == null)
                return;

            var currentPlan = DayPeriodManager.Instance.CurrentPeriodConfig;
            var prevPlan = DayPeriodManager.Instance.PreviousPeriodConfig;
            var timer = DayPeriodManager.Instance.PeriodTimer;

            // Если конфиги еще не загрузились
            if (currentPlan == null || prevPlan == null)
                return;

            var duration = currentPlan.durationInSeconds;
            if (duration <= 0)
                return;

            // Вычисляем прогресс времени от 0.0 до 1.0
            var progress = Mathf.Clamp01(timer / duration);

            // Интерполируем (смешиваем) цвет и яркость между предыдущим и текущим периодом
            globalLight.color = Color.Lerp(prevPlan.lightingSettings.lightColor, currentPlan.lightingSettings.lightColor, progress);
            globalLight.intensity = Mathf.Lerp(prevPlan.lightingSettings.lightIntensity, currentPlan.lightingSettings.lightIntensity, progress);
        }

        // Этот метод вызывается автоматически при смене периода (Утро -> День и т.д.)
        private void HandlePeriodChange()
        {
            var currentPlan = DayPeriodManager.Instance.CurrentPeriodConfig;
            if (currentPlan == null) return;

            // 1. Включаем/выключаем фонарики у персонала (если Ночь)
            bool isNight = DayPeriodManager.Instance.CurrentPeriodType.IsNight();
            ToggleStaffLights(isNight);

            // 2. Управляем лампами в офисе (включаем те, что прописаны в календаре)
            if (lightManagementCoroutine != null) StopCoroutine(lightManagementCoroutine);
            lightManagementCoroutine = StartCoroutine(ManageLocalLightsSmoothly(currentPlan));
        }

        private void ToggleStaffLights(bool enable)
        {
            // Ищем всех сотрудников на сцене
            var allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None);
            foreach (var staff in allStaff)
            {
                // Если это охранник и у него есть фонарик
                if (staff is GuardMovement guard && guard.nightLight != null)
                    guard.nightLight.SetActive(enable);
                // Если это уборщик и у него есть фонарик
                else if (staff is ServiceWorkerController worker && worker.nightLight != null)
                    worker.nightLight.SetActive(enable);
            }
        }

        private IEnumerator ManageLocalLightsSmoothly(PeriodSettings periodPlan)
        {
            // Создаем список имен ламп, которые ДОЛЖНЫ гореть в этом периоде
            var lightsToEnable = new HashSet<string>(periodPlan.lightsToEnableNames);

            foreach (var lightGO in allControllableLights)
            {
                if (lightGO == null) continue;

                bool shouldBeOn = lightsToEnable.Contains(lightGO.name);
                bool isCurrentlyOn = lightGO.activeSelf;

                // Если лампа выключена, а должна гореть
                if (shouldBeOn && !isCurrentlyOn)
                {
                    // Маленькая случайная задержка для эффекта "включения по очереди"
                    yield return new WaitForSeconds(Random.Range(0f, 0.5f));
                    StartCoroutine(FadeLight(lightGO, true));
                }
                // Если лампа горит, а должна быть выключена
                else if (!shouldBeOn && isCurrentlyOn)
                {
                    StartCoroutine(FadeLight(lightGO, false));
                }
            }
        }

        // Корутина для плавного включения/выключения отдельной лампы
        private IEnumerator FadeLight(GameObject lightObject, bool turnOn)
        {
            var lightSource = lightObject.GetComponent<Light2D>();
            
            // Если на объекте нет Light2D (просто спрайт), переключаем мгновенно
            if (lightSource == null)
            {
                lightObject.SetActive(turnOn);
                yield break;
            }

            float startIntensity = turnOn ? 0f : lightSource.intensity;
            float endIntensity = turnOn ? 1f : 0f; // Здесь можно брать intensity из настроек лампы, если нужно
            float timer = 0f;

            if (turnOn)
            {
                lightSource.intensity = 0;
                lightObject.SetActive(true);
            }

            while (timer < lightFadeDuration)
            {
                timer += Time.deltaTime;
                lightSource.intensity = Mathf.Lerp(startIntensity, endIntensity, timer / lightFadeDuration);
                yield return null;
            }

            lightSource.intensity = endIntensity;
            if (!turnOn) lightObject.SetActive(false);
        }
    }
}
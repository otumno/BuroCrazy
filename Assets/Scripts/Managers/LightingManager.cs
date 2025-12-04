using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Data.Calendar;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Managers
{
    public class LightingManager : MonoBehaviour
    {
        public static LightingManager Instance { get; private set; }

        [Header("Глобальный Свет")]
        public Light2D globalLight;
        
        [Header("Лампы в Офисе")]
        public List<GameObject> allControllableLights;
        
        [Header("Настройки")]
        public float lightFadeDuration = 1.0f;

        private Coroutine lightTransitionCoroutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            TimeManager.Instance.OnPeriodChanged += OnPeriodChanged;

            var currentSettings = TimeManager.Instance.GetCurrentPeriodSettings();
            if (currentSettings != null)
            {
                // Применяем свет МГНОВЕННО (true), чтобы при старте не было "перетекания"
                ApplyLightingSettings(currentSettings, true);
            }
        }

        private void OnDestroy()
        {
            TimeManager.Instance.OnPeriodChanged -= OnPeriodChanged;
            Instance = null;
        }

        // Обработчик события (вызывается при смене периода во время игры)
        private void OnPeriodChanged(PeriodSettings newSettings)
        {
            // При смене периода используем плавный переход (false)
            ApplyLightingSettings(newSettings, false);
        }

        private void ApplyLightingSettings(PeriodSettings settings, bool isInstant)
        {
            if (settings == null)
                return;

            // 1. Глобальный свет
            if (lightTransitionCoroutine != null) StopCoroutine(lightTransitionCoroutine);
            
            if (isInstant)
            {
                // Мгновенная установка (для старта игры)
                if (globalLight != null)
                {
                    globalLight.color = settings.lightingSettings.lightColor;
                    globalLight.intensity = settings.lightingSettings.lightIntensity;
                }
            }
            else
            {
                // Плавный переход (для геймплея)
                lightTransitionCoroutine = StartCoroutine(TransitionGlobalLight(settings));
            }

            // 2. Лампы
            ManageLocalLights(settings, isInstant);

            // 3. Фонарики персонала
            bool isNight = settings.PeriodType.IsNight();
            ToggleStaffLights(isNight);
        }

        private IEnumerator TransitionGlobalLight(PeriodSettings targetSettings)
        {
            if (globalLight == null) yield break;

            Color startColor = globalLight.color;
            float startIntensity = globalLight.intensity;
            
            Color targetColor = targetSettings.lightingSettings.lightColor;
            float targetIntensity = targetSettings.lightingSettings.lightIntensity;

            float timer = 0f;
            while (timer < lightFadeDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / lightFadeDuration;

                globalLight.color = Color.Lerp(startColor, targetColor, progress);
                globalLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, progress);
                yield return null;
            }

            globalLight.color = targetColor;
            globalLight.intensity = targetIntensity;
        }

        private void ManageLocalLights(PeriodSettings periodPlan, bool isInstant)
        {
            var lightsToEnable = new HashSet<string>(periodPlan.lightsToEnableNames);

            foreach (var lightGO in allControllableLights)
            {
                if (lightGO == null) continue;

                bool shouldBeOn = lightsToEnable.Contains(lightGO.name);
                bool isCurrentlyOn = lightGO.activeSelf;

                if (isInstant)
                {
                    // Мгновенно включаем/выключаем без корутин
                    if (shouldBeOn != isCurrentlyOn)
                    {
                        lightGO.SetActive(shouldBeOn);
                        // Если на объекте есть Light2D, сбрасываем его интенсивность на норму
                        var lightSource = lightGO.GetComponent<Light2D>();
                        if (lightSource != null && shouldBeOn) lightSource.intensity = 1f;
                    }
                }
                else
                {
                    // Плавное переключение
                    if (shouldBeOn && !isCurrentlyOn)
                    {
                        StartCoroutine(FadeLocalLight(lightGO, true));
                    }
                    else if (!shouldBeOn && isCurrentlyOn)
                    {
                        StartCoroutine(FadeLocalLight(lightGO, false));
                    }
                }
            }
        }

        private IEnumerator FadeLocalLight(GameObject lightObj, bool turnOn)
        {
            yield return new WaitForSeconds(Random.Range(0f, 0.5f));

            var lightSource = lightObj.GetComponent<Light2D>();
            if (lightSource == null)
            {
                lightObj.SetActive(turnOn);
                yield break;
            }

            float startInt = turnOn ? 0f : lightSource.intensity;
            float endInt = turnOn ? 1f : 0f;
            
            if (turnOn) lightObj.SetActive(true);

            float timer = 0f;
            while (timer < 0.5f)
            {
                timer += Time.deltaTime;
                lightSource.intensity = Mathf.Lerp(startInt, endInt, timer / 0.5f);
                yield return null;
            }

            if (!turnOn) lightObj.SetActive(false);
            lightSource.intensity = endInt;
        }

        private void ToggleStaffLights(bool enable)
        {
            var allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None);
            foreach (var staff in allStaff)
            {
                if (staff is GuardMovement guard && guard.nightLight != null)
                    guard.nightLight.SetActive(enable);
                else if (staff is ServiceWorkerController worker && worker.nightLight != null)
                    worker.nightLight.SetActive(enable);
                else if (staff is DirectorAvatarController director && director.nightLight != null)
                    director.nightLight.SetActive(enable);
            }
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Data.Calendar;
using Scriptables;
using UnityEngine;
using UnityEngine.Rendering.Universal; // Для Light2D

namespace Managers
{
    public class LightManager : MonoBehaviour
    {
        public static LightManager Instance { get; private set; }

        [Header("Глобальное освещение")]
        public Light2D globalLight;
        public float lightFadeDuration = 0.5f;

        [Header("Локальные источники света (лампы)")]
        public List<GameObject> allControllableLights;

        private Coroutine lightManagementCoroutine;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); } else { Instance = this; }
        }

        void Start()
        {
            // Подписываемся на смену периода в спавнере
            if (ClientSpawner.Instance != null)
            {
                ClientSpawner.Instance.OnPeriodChanged += HandlePeriodChange;
            }
        }

        void OnDestroy()
        {
            if (ClientSpawner.Instance != null)
            {
                ClientSpawner.Instance.OnPeriodChanged -= HandlePeriodChange;
            }
        }

        void Update()
        {
            // Плавная смена глобального света (Global Light)
            UpdateGlobalLighting();
        }

        private void UpdateGlobalLighting()
        {
            if (globalLight == null || ClientSpawner.Instance == null) return;

            var currentPlan = ClientSpawner.Instance.GetCurrentPeriodPlan();
            var prevPlan = ClientSpawner.Instance.GetPreviousPeriodPlan();
            float timer = ClientSpawner.Instance.GetPeriodTimer();

            if (currentPlan == null || prevPlan == null) return;

            float duration = currentPlan.durationInSeconds;
            if (duration <= 0) return;

            // Вычисляем прогресс от 0 до 1
            float progress = Mathf.Clamp01(timer / duration);

            // Интерполируем цвет и интенсивность между прошлым периодом и текущим
            globalLight.color = Color.Lerp(prevPlan.lightingSettings.lightColor, currentPlan.lightingSettings.lightColor, progress);
            globalLight.intensity = Mathf.Lerp(prevPlan.lightingSettings.lightIntensity, currentPlan.lightingSettings.lightIntensity, progress);
        }

        // Вызывается событием из ClientSpawner
        private void HandlePeriodChange()
        {
            var currentPlan = ClientSpawner.Instance.GetCurrentPeriodPlan();
            if (currentPlan == null) return;

            // 1. Включаем/выключаем фонарики у персонала
            ToggleStaffLights(ClientSpawner.CurrentPeriodType.IsNight());

            // 2. Управляем лампами в офисе
            if (lightManagementCoroutine != null) StopCoroutine(lightManagementCoroutine);
            lightManagementCoroutine = StartCoroutine(ManageLocalLightsSmoothly(currentPlan));
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
            }
        }

        private IEnumerator ManageLocalLightsSmoothly(PeriodSettings periodPlan)
        {
            // Собираем список ламп, которые ДОЛЖНЫ гореть в этом периоде
            var lightsToEnable = new HashSet<GameObject>();
            foreach (var lightName in periodPlan.lightsToEnableNames)
            {
                var lightObj = allControllableLights.FirstOrDefault(l => l.name == lightName);
                if (lightObj != null) lightsToEnable.Add(lightObj);
            }

            foreach (var lightGO in allControllableLights)
            {
                if (lightGO == null) continue;

                bool shouldBeOn = lightsToEnable.Contains(lightGO);
                bool isCurrentlyOn = lightGO.activeSelf;

                if (shouldBeOn && !isCurrentlyOn)
                {
                    // Случайная задержка для эффекта "включения по очереди"
                    yield return new WaitForSeconds(Random.Range(0f, 0.5f));
                    StartCoroutine(FadeLight(lightGO, true));
                }
                else if (!shouldBeOn && isCurrentlyOn)
                {
                    StartCoroutine(FadeLight(lightGO, false));
                }
            }
        }

        private IEnumerator FadeLight(GameObject lightObject, bool turnOn)
        {
            var lightSource = lightObject.GetComponent<Light2D>();
            if (lightSource == null)
            {
                // Если нет компонента Light2D, просто включаем/выключаем объект
                lightObject.SetActive(turnOn);
                yield break;
            }

            float startIntensity = turnOn ? 0f : lightSource.intensity;
            float endIntensity = turnOn ? 1f : 0f; // Можно хардкодить 1 или брать из пресета, пока так
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
// Assets/Scripts/Managers/LightingManager.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Data.Calendar;
using UnityEngine;
using UnityEngine.Rendering.Universal; // Обязательно для Light2D
using Scriptables.Audio;
// using Characters.Controllers; // Убрали, чтобы не было ошибки пространства имен

namespace Managers
{
    public class LightingManager : MonoBehaviour
    {
        public static LightingManager Instance { get; private set; }

        [Header("Глобальный Свет")]
        public Light2D globalLight;
        
        [Header("Лампы в Офисе")]
        public List<GameObject> allControllableLights;
        
        [Header("Тайминги")]
        [Tooltip("Длительность плавного перехода глобального света")]
        public float lightFadeDuration = 1.0f;

        [Tooltip("Задержка перед тем, как лампы начнут переключаться после смены периода")]
        public float periodStartDelay = 1.5f;

        [Tooltip("Максимальный разброс времени включения отдельных ламп.")]
        public float maxLampDelay = 2.5f; 
        
        [Header("Звуки")]
        public SoundID masterSwitchSound = SoundID.Light_MasterSwitch;
        public SoundID lampTwinkleSound = SoundID.Light_LampTwinkle;
        public float soundDelayAfterVisual = 0.1f;
        [Range(0f, 1f)] public float lampSoundChance = 0.33f;

        private Coroutine lightTransitionCoroutine;
        private Coroutine lampsRoutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnPeriodChanged += OnPeriodChanged;
                var currentSettings = TimeManager.Instance.GetCurrentPeriodSettings();
                if (currentSettings != null)
                {
                    ApplyLightingSettings(currentSettings, true);
                }
            }
        }

        private void OnPeriodChanged(PeriodSettings newSettings)
        {
            ApplyLightingSettings(newSettings, false);
        }

        private void ApplyLightingSettings(PeriodSettings settings, bool isInstant)
        {
            if (settings == null) return;

            // --- 1. ГЛОБАЛЬНЫЙ СВЕТ ---
            if (lightTransitionCoroutine != null) StopCoroutine(lightTransitionCoroutine);
            
            if (isInstant)
            {
                if (globalLight != null)
                {
                    globalLight.color = settings.lightingSettings.lightColor;
                    globalLight.intensity = settings.lightingSettings.lightIntensity;
                }
            }
            else
            {
                StartCoroutine(PlayMasterSwitchWithDelay(periodStartDelay));
                lightTransitionCoroutine = StartCoroutine(TransitionGlobalLight(settings));
            }

            // --- 2. ЛАМПЫ ---
            if (lampsRoutine != null) StopCoroutine(lampsRoutine);
            lampsRoutine = StartCoroutine(ManageLocalLightsRoutine(settings, isInstant));

            // --- 3. ФОНАРИКИ ПЕРСОНАЛА ---
            bool isNight = settings.PeriodType.IsNight();
            ToggleStaffLights(isNight);
        }

        private IEnumerator PlayMasterSwitchWithDelay(float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            
            if (AudioManager.Instance != null && masterSwitchSound != SoundID.None)
            {
                AudioManager.Instance.PlaySound(masterSwitchSound);
            }
        }

        private IEnumerator TransitionGlobalLight(PeriodSettings targetSettings)
        {
            if (globalLight == null) yield break;

            yield return new WaitForSeconds(periodStartDelay);

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

        private IEnumerator ManageLocalLightsRoutine(PeriodSettings periodPlan, bool isInstant)
        {
            if (!isInstant && periodStartDelay > 0)
            {
                yield return new WaitForSeconds(periodStartDelay);
            }

            var lightsToEnable = new HashSet<string>(periodPlan.lightsToEnableNames);

            foreach (var lightGO in allControllableLights)
            {
                if (lightGO == null) continue;

                bool shouldBeOn = lightsToEnable.Contains(lightGO.name);
                bool isCurrentlyOn = lightGO.activeSelf;

                if (isInstant)
                {
                    if (shouldBeOn != isCurrentlyOn)
                    {
                        lightGO.SetActive(shouldBeOn);
                        var lightSource = lightGO.GetComponent<Light2D>();
                        if (lightSource != null && shouldBeOn) lightSource.intensity = 1f;
                    }
                }
                else
                {
                    if (shouldBeOn != isCurrentlyOn)
                    {
                        StartCoroutine(FadeLocalLight(lightGO, shouldBeOn));
                    }
                }
            }
        }

        private IEnumerator FadeLocalLight(GameObject lightObj, bool turnOn)
        {
            yield return new WaitForSeconds(Random.Range(0f, maxLampDelay));

            var lightSource = lightObj.GetComponent<Light2D>();
            
            if (lightSource == null)
            {
                lightObj.SetActive(turnOn);
                yield return PlayLampSoundWithDelay(lightObj); 
                yield break;
            }

            float startInt = turnOn ? 0f : lightSource.intensity;
            float endInt = turnOn ? 1f : 0f;
            
            if (turnOn) lightObj.SetActive(true);

            StartCoroutine(PlayLampSoundWithDelay(lightObj));

            float timer = 0f;
            while (timer < 0.4f)
            {
                timer += Time.deltaTime;
                lightSource.intensity = Mathf.Lerp(startInt, endInt, timer / 0.4f);
                yield return null;
            }

            if (!turnOn) lightObj.SetActive(false);
            lightSource.intensity = endInt;
        }

        private IEnumerator PlayLampSoundWithDelay(GameObject lightObj)
        {
            if (Random.value > lampSoundChance) yield break;

            if (soundDelayAfterVisual > 0) 
                yield return new WaitForSeconds(soundDelayAfterVisual);

            if (AudioManager.Instance != null && lampTwinkleSound != SoundID.None)
            {
                AudioManager.Instance.PlaySound(lampTwinkleSound, lightObj.transform.position);
            }
        }

        // --- ИСПРАВЛЕННЫЙ МЕТОД (БЕЗ ТЕГОВ [source]) ---
        private void ToggleStaffLights(bool enable)
        {
            var allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None);
            
            foreach (var staff in allStaff)
            {
                // 1. ОХРАННИКИ (У них nightLight это Light2D)
                var guard = staff.GetComponent<GuardMovement>();
                if (guard != null && guard.nightLight != null)
                {
                    guard.nightLight.enabled = enable; // Используем enabled
                    continue;
                }

                // 2. РАБОЧИЕ (У них nightLight это GameObject)
                var worker = staff.GetComponent<ServiceWorkerController>();
                if (worker != null && worker.nightLight != null)
                {
                    worker.nightLight.SetActive(enable); // Используем SetActive
                    continue;
                }

                // 3. ДИРЕКТОР (У него nightLight это GameObject)
                var director = staff.GetComponent<DirectorAvatarController>();
                if (director != null && director.nightLight != null)
                {
                    director.nightLight.SetActive(enable); // Используем SetActive
                    continue;
                }
            }
        }
        
        private void OnDestroy()
        {
            if (TimeManager.Instance)
                TimeManager.Instance.OnPeriodChanged -= OnPeriodChanged;
            
            Instance = null;
        }
    }
}
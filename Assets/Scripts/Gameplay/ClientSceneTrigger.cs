using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Data;
using Enums;
using Characters;
using Data.Calendar;
using Managers;
using System.Linq;

public class ClientSceneTrigger : MonoBehaviour
{
    [Header("=== НАСТРОЙКИ ===")]
    [Tooltip("Радиус проверки клиентов")]
    public float checkRadius = 5f;

    [Tooltip("Ссылка на базу сценок")]
    public List<ClientSceneData> availableScenes = new List<ClientSceneData>();

    [Tooltip("Искать сценки в Resources автоматически")]
    public bool loadFromResources = true;

    [Header("=== ССЫЛКИ ===")]
    public string associatedZoneTag;

    private float lastCheckTime;
    private float lastSceneTime;
    private Dictionary<string, float> sceneCooldowns = new Dictionary<string, float>();

    private void Start()
    {
        if (loadFromResources)
        {
            LoadScenesFromResources();
        }
    }

    private void LoadScenesFromResources()
    {
        var scenes = Resources.LoadAll<ClientSceneData>("ClientScenes");
        foreach (var scene in scenes)
        {
            if (!availableScenes.Contains(scene))
            {
                availableScenes.Add(scene);
            }
        }
        if (availableScenes.Count > 0)
        {
            Debug.Log($"[ClientSceneTrigger] Загружено {availableScenes.Count} сценок для зоны {gameObject.name}");
        }
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return;

        if (Time.time - lastCheckTime > 5f)
        {
            CheckForSceneTrigger();
            lastCheckTime = Time.time;
        }
    }

    private void CheckForSceneTrigger()
    {
        var nearbyClients = GetNearbyClients();
        if (nearbyClients.Count < 2) return;

        var currentPeriod = TimeManager.Instance != null ? TimeManager.Instance.GetCurrentPeriodType() : CalendarDayPeriodType.Day;
        float normalizedTime = GetNormalizedTime(currentPeriod);

        foreach (var scene in availableScenes)
        {
            // Проверяем кулдаун
            if (sceneCooldowns.TryGetValue(scene.sceneID, out float cooldownEnd) && Time.time < cooldownEnd)
                continue;

            // Проверяем условия
            if (!CanTriggerScene(scene, nearbyClients, normalizedTime))
                continue;

            // Проверяем шанс
            if (Random.value > scene.triggerChance)
                continue;

            // Запускаем сценку
            StartCoroutine(PlayScene(scene, nearbyClients));
            break;
        }
    }

    private bool CanTriggerScene(ClientSceneData scene, List<ClientPathfinding> clients, float normalizedTime)
    {
        // Проверка минимум клиентов
        if (clients.Count < scene.minClientsInZone)
            return false;

        // Проверка времени
        if (scene.timePreference != null && scene.timePreference.Evaluate(normalizedTime) < 0.1f)
            return false;

        // Проверка архетипов
        int eligibleClients = 0;
        foreach (var client in clients)
        {
            var archetype = client.GetVisuals()?.currentArchetype;
            if (archetype == null) continue;
            if (IsArchetypeAllowed(archetype, scene))
            {
                eligibleClients++;
            }
        }

        return eligibleClients >= Mathf.Max(2, scene.minClientsInZone / 2);
    }

    private bool IsArchetypeAllowed(ClientArchetype archetype, ClientSceneData scene)
    {
        // Проверяем исключения
        if (scene.excludeArchetypes.Contains(archetype.groupID))
            return false;

        // Проверяем разрешения (если есть)
        if (scene.allowedArchetypes.Count > 0 && !scene.allowedArchetypes.Contains(archetype.groupID))
            return false;

        return true;
    }

    private List<ClientPathfinding> GetNearbyClients()
    {
        var result = new List<ClientPathfinding>();
        var colliders = Physics2D.OverlapCircleAll(transform.position, checkRadius);

        foreach (var collider in colliders)
        {
            var client = collider.GetComponent<ClientPathfinding>();
            if (client != null && client.stateMachine != null)
            {
                var state = client.stateMachine.GetCurrentState();
                if (state != ClientState.Leaving && state != ClientState.LeavingUpset)
                {
                    result.Add(client);
                }
            }
        }

        return result;
    }

    private IEnumerator PlayScene(ClientSceneData scene, List<ClientPathfinding> participants)
    {
        if (scene.debugMode)
        {
            Debug.Log($"[ClientScene] Запуск сценки: {scene.sceneName ?? scene.sceneID}");
        }

        // Устанавливаем кулдаун
        sceneCooldowns[scene.sceneID] = Time.time + scene.sceneCooldown;
        lastSceneTime = Time.time;

        // Выбираем участников
        var activeParticipants = SelectParticipants(scene, participants);

        // Воспроизводим реплики
        foreach (var line in scene.lines)
        {
            var speaker = FindSpeaker(line, activeParticipants);
            if (speaker != null)
            {
                speaker.ShowThoughtBubble(line.text, scene.lineDuration);
                if (scene.debugMode)
                {
                    Debug.Log($"[ClientScene] {speaker.name}: {line.text}");
                }
            }

            yield return new WaitForSeconds(scene.lineDelay);
        }

        // Применяем эффекты
        if (scene.causeGrumbling)
        {
            foreach (var participant in participants)
            {
                participant.AddStress(scene.grumblingMultiplier * 5f);
            }
        }

        if (scene.affectEmotions)
        {
            foreach (var participant in activeParticipants)
            {
                var visuals = participant.GetVisuals();
                if (visuals != null)
                {
                    visuals.SetEmotionForState(scene.postSceneEmotion);
                }
            }
        }
    }

    private List<ClientPathfinding> SelectParticipants(ClientSceneData scene, List<ClientPathfinding> allClients)
    {
        var selected = new List<ClientPathfinding>();

        foreach (var line in scene.lines)
        {
            if (string.IsNullOrEmpty(line.speakerArchetypeID))
            {
                // Любой участник
                foreach (var client in allClients)
                {
                    if (!selected.Contains(client))
                    {
                        selected.Add(client);
                        break;
                    }
                }
            }
            else
            {
                // Ищем по архетипу
                foreach (var client in allClients)
                {
                    var archetype = client.GetVisuals()?.currentArchetype;
                    if (archetype != null &&
                        archetype.groupID == line.speakerArchetypeID &&
                        !selected.Contains(client))
                    {
                        selected.Add(client);
                        break;
                    }
                }
            }
        }

        return selected.Count > 0 ? selected : allClients.Take(2).ToList();
    }

    private ClientPathfinding FindSpeaker(SceneLine line, List<ClientPathfinding> participants)
    {
        foreach (var participant in participants)
        {
            var archetype = participant.GetVisuals()?.currentArchetype;
            if (archetype == null) continue;

            // Проверяем архетип
            if (!string.IsNullOrEmpty(line.speakerArchetypeID))
            {
                if (archetype.groupID != line.speakerArchetypeID)
                    continue;
            }

            // Проверяем пол
            if (line.requiredGender >= 0)
            {
                var gender = (int)participant.gender;
                if (gender != line.requiredGender)
                    continue;
            }

            return participant;
        }

        return participants.Count > 0 ? participants[0] : null;
    }

    private float GetNormalizedTime(CalendarDayPeriodType period)
    {
        // Простая нормализация: ночь = 0, день = 0.5-1.0
        if ((period & CalendarDayPeriodType.StartNight) != 0) return 0f;
        if ((period & CalendarDayPeriodType.EndNight) != 0) return 0.1f;
        if ((period & CalendarDayPeriodType.Morning) != 0) return 0.8f;
        if ((period & CalendarDayPeriodType.EarlyDay) != 0) return 0.9f;
        if ((period & CalendarDayPeriodType.Noon) != 0) return 1f;
        if ((period & CalendarDayPeriodType.Day) != 0) return 0.8f;
        if ((period & CalendarDayPeriodType.LateDay) != 0) return 0.6f;
        if ((period & CalendarDayPeriodType.Evening) != 0) return 0.3f;
        return 0.5f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, checkRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawIcon(transform.position, "d_UnityEditor.Console", true);
    }
#endif
}

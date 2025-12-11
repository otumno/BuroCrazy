using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Managers;

[RequireComponent(typeof(ClientPathfinding))]
public class ClientMessGenerator : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float baseTrashChancePerSecond = 0.01f;
    [SerializeField] private float puddleChanceOnUpset = 0.3f;

    private ClientPathfinding _client;
    private Coroutine _generationCoroutine;
    private bool _isActive = true;

    public void Initialize(ClientPathfinding client)
    {
        _client = client;
        // Запускаем процесс автоматически при инициализации
        StartGeneration();
    }

    public void SetActive(bool isActive)
    {
        _isActive = isActive;
    }

    private void StartGeneration()
    {
        if (_generationCoroutine != null) StopCoroutine(_generationCoroutine);
        _generationCoroutine = StartCoroutine(TrashGenerationRoutine());
    }

    private IEnumerator TrashGenerationRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            if (!_isActive || _client == null) continue;
            
            // Не мусорим, если уходим (если это не спец. логика) или если клиент отключен
            var state = _client.stateMachine.GetCurrentState();
            if (state == ClientState.Leaving || state == ClientState.LeavingUpset) continue;

            if (_client.trashPrefabs == null || _client.trashPrefabs.Count == 0) continue;

            if (MessManager.Instance != null && MessManager.Instance.CanCreateMess())
            {
                // Формула шанса (зависит от черт характера)
                float chance = baseTrashChancePerSecond 
                             + (_client.suetunFactor * 0.02f) 
                             - (_client.babushkaFactor * 0.005f);

                if (Random.value < chance)
                {
                    SpawnTrash();
                }
            }
        }
    }

    private void SpawnTrash()
    {
        GameObject randomTrashPrefab = _client.trashPrefabs[Random.Range(0, _client.trashPrefabs.Count)];
        Vector2 spawnPosition = (Vector2)transform.position;
        
        // Логика поиска урны (оставляем как было, но изолируем)
        TrashCan closestCan = null;
        float minDistance = float.MaxValue;
        
        foreach (var can in TrashCan.AllTrashCans)
        {
            if (can.IsFull) continue;
            float distance = Vector2.Distance(transform.position, can.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestCan = can;
            }
        }

        if (closestCan != null && minDistance <= closestCan.attractionRadius)
        {
            spawnPosition = (Vector2)closestCan.transform.position + (Random.insideUnitCircle * 0.5f);
            closestCan.AddTrash();
        }
        else
        {
            spawnPosition += Random.insideUnitCircle * 0.2f;
        }

        Instantiate(randomTrashPrefab, spawnPosition, Quaternion.identity);
    }

    public void TrySpawnPuddle()
    {
        if (_client.puddlePrefabs != null && _client.puddlePrefabs.Count > 0)
        {
            if (MessManager.Instance != null && MessManager.Instance.CanCreateMess() && Random.value < puddleChanceOnUpset)
            {
                GameObject randomPuddlePrefab = _client.puddlePrefabs[Random.Range(0, _client.puddlePrefabs.Count)];
                Instantiate(randomPuddlePrefab, transform.position, Quaternion.identity);
            }
        }
    }
}
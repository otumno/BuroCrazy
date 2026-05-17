// === FILE: Assets/Scripts/Cinematic/Nodes/SpawnCharacterNode.cs ===
using System.Collections;
using UnityEngine;
using Managers;
using Characters;
using Data;

namespace CinematicSystem.Nodes
{
    /// <summary>
    /// Узел спавна персонажа (клиента) на сцене.
    /// Спавнит клиента указанного архетипа в указанной точке.
    /// </summary>
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Spawn Character")]
    public class SpawnCharacterNode : NextNode
    {
        [Tooltip("ID архетипа из ArchetypeDatabase")]
        public string archetypeID;
        
        [Tooltip("Ключ точки спавна в SceneObjectRegistry")]
        public string spawnPointKey;
        
        [Tooltip("Ключ для сохранения ссылки на персонажа")]
        public string targetKeyForReference;
        
        [Tooltip("Принудительная цель клиента")]
        public ClientGoal forcedGoal = ClientGoal.AskAndLeave;

        public override string GetNodeType() => "spawn";

        public override IEnumerator Execute(CinematicPlayer player)
        {
            // Загружаем базу архетипов
            var db = Resources.Load<ArchetypeDatabase>("Databases/ArchetypeDatabase");
            if (db == null)
            {
                Debug.LogError("[SpawnCharacterNode] ArchetypeDatabase не найден");
                player.GoToNextNode(nextNode);
                yield break;
            }

            var archetype = db.GetArchetypeByID(archetypeID);
            if (archetype == null)
            {
                Debug.LogError($"[SpawnCharacterNode] Архетип {archetypeID} не найден");
                player.GoToNextNode(nextNode);
                yield break;
            }

            var spawnPos = SceneObjectRegistry.Instance.GetTransform(spawnPointKey);
            if (spawnPos == null)
            {
                Debug.LogError($"[SpawnCharacterNode] Точка спавна {spawnPointKey} не найдена");
                player.GoToNextNode(nextNode);
                yield break;
            }

            // Используем WaveManager для спавна
            var waveManager = WaveManager.Instance;
            if (waveManager == null || waveManager.clientPrefab == null)
            {
                Debug.LogError("[SpawnCharacterNode] WaveManager или clientPrefab не найдены");
                player.GoToNextNode(nextNode);
                yield break;
            }

            // Спавним клиента
            GameObject clientObj = UnityEngine.Object.Instantiate(waveManager.clientPrefab, spawnPos.position, Quaternion.identity);
            ClientPathfinding client = clientObj.GetComponent<ClientPathfinding>();
            
            if (client != null)
            {
                client.mainGoal = forcedGoal;
                client.SetupFromArchetype(archetype);
                client.SetupGrumblingFromArchetype(archetype);
                
                var visuals = client.GetComponent<CharacterVisuals>();
                if (visuals != null) visuals.SetupVisualDiversity(archetype);
                
                client.Initialize(waveManager.waitingZoneObject, waveManager.exitWaypoint);
                
                // Регистрируем в CharacterRegistry
                if (!string.IsNullOrEmpty(targetKeyForReference))
                {
                    CharacterRegistry.Instance.Register(targetKeyForReference, client);
                }
            }

            player.GoToNextNode(nextNode);
        }
    }
}
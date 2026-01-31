using System.Collections.Generic;
using UnityEngine;

namespace Data
{
    [CreateAssetMenu(fileName = "ArchetypeDatabase", menuName = "Bureau/Databases/Archetype Database")]
    public class ArchetypeDatabase : ScriptableObject
    {
        [Header("Все архетипы клиентов")]
        public List<Characters.ClientArchetype> allArchetypes;

        [Header("Веса спавна (сумма должна быть примерно 100)")]
        public List<ArchetypeWeight> spawnWeights;

        [System.Serializable]
        public class ArchetypeWeight
        {
            public Characters.ClientArchetype archetype;
            [Tooltip("Вес при спавне (чем выше, тем чаще появляется)")]
            public float weight = 10f;
        }

        public Characters.ClientArchetype GetRandomArchetype()
        {
            if (allArchetypes == null || allArchetypes.Count == 0)
            {
                Debug.LogError("[ArchetypeDatabase] База архетипов пуста!");
                return null;
            }

            if (spawnWeights == null || spawnWeights.Count == 0)
            {
                return allArchetypes[Random.Range(0, allArchetypes.Count)];
            }

            float totalWeight = 0f;
            foreach (var item in spawnWeights)
            {
                if (item.archetype != null)
                {
                    totalWeight += item.weight;
                }
            }

            float randomPoint = Random.Range(0, totalWeight);
            float currentWeight = 0f;

            foreach (var item in spawnWeights)
            {
                if (item.archetype == null) continue;

                currentWeight += item.weight;
                if (randomPoint <= currentWeight)
                {
                    return item.archetype;
                }
            }

            return allArchetypes[0];
        }

        public Characters.ClientArchetype GetArchetypeByID(string id)
        {
            if (allArchetypes == null) return null;

            foreach (var archetype in allArchetypes)
            {
                if (archetype != null && archetype.archetypeID == id)
                {
                    return archetype;
                }
            }

            Debug.LogWarning($"[ArchetypeDatabase] Архетип с ID '{id}' не найден!");
            return null;
        }

        public List<Characters.ClientArchetype> GetArchetypesWithGoal(ClientGoal goal)
        {
            var result = new List<Characters.ClientArchetype>();

            if (allArchetypes == null) return result;

            foreach (var archetype in allArchetypes)
            {
                if (archetype != null && archetype.allowedGoals.Contains(goal))
                {
                    result.Add(archetype);
                }
            }

            return result;
        }

        public Characters.ClientArchetype GetRandomByGroup(string groupID)
        {
            var groupArchetypes = GetByGroup(groupID);
            if (groupArchetypes.Count == 0)
            {
                Debug.LogWarning($"[ArchetypeDatabase] GetRandomByGroup('{groupID}'): Архетипы группы не найдены! Falling back to GetRandomArchetype().");
                var fallback = GetRandomArchetype();
                if (fallback != null)
                {
                    Debug.LogWarning($"[ArchetypeDatabase]   Fallback returned: {fallback.name} (groupID: {fallback.groupID})");
                }
                return fallback;
            }

            var selected = groupArchetypes[Random.Range(0, groupArchetypes.Count)];
            Debug.Log($"[ArchetypeDatabase] GetRandomByGroup('{groupID}'): Selected '{selected.name}' (groupID: {selected.groupID})");
            return selected;
        }

        public List<Characters.ClientArchetype> GetByGroup(string groupID)
        {
            var result = new List<Characters.ClientArchetype>();

            if (allArchetypes == null)
            {
                Debug.LogWarning($"[ArchetypeDatabase] GetByGroup('{groupID}'): allArchetypes is null!");
                return result;
            }

            Debug.Log($"[ArchetypeDatabase] GetByGroup('{groupID}'): Searching through {allArchetypes.Count} archetypes...");

            foreach (var archetype in allArchetypes)
            {
                if (archetype != null)
                {
                    bool matches = archetype.groupID == groupID;
                    Debug.Log($"[ArchetypeDatabase]   Archetype '{archetype.name}' has groupID '{archetype.groupID}' - Match: {matches}");
                    if (matches)
                    {
                        result.Add(archetype);
                    }
                }
            }

            Debug.Log($"[ArchetypeDatabase] GetByGroup('{groupID}'): Found {result.Count} archetypes");
            return result;
        }
    }
}

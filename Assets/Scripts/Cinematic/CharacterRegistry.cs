// === FILE: Assets/Scripts/Cinematic/CharacterRegistry.cs ===
using System.Collections.Generic;
using UnityEngine;
using Characters;
using Managers;

namespace CinematicSystem
{
    /// <summary>
    /// Реестр живых персонажей для доступа по ID.
    /// </summary>
    public class CharacterRegistry : MonoBehaviour
    {
        /// <summary>Singleton instance</summary>
        public static CharacterRegistry Instance { get; private set; }

        /// <summary>Кэш зарегистрированных персонажей</summary>
        private Dictionary<string, MonoBehaviour> characters = new Dictionary<string, MonoBehaviour>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Получить персонажа по ID.
        /// </summary>
        public MonoBehaviour GetCharacter(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            // Директор (игрок)
            if (id == "Director")
                return DirectorAvatarController.Instance;

            // Сотрудники
            if (id.StartsWith("Staff:"))
            {
                var param = id.Substring(6);
                var allStaff = HiringManager.Instance?.AllStaff;
                if (allStaff == null) return null;

                // Поиск по индексу
                if (int.TryParse(param, out int idx) && idx >= 0 && idx < allStaff.Count)
                    return allStaff[idx];

                // Поиск по имени или роли
                foreach (var s in allStaff)
                {
                    if (s != null && s.characterName != null && s.characterName.Contains(param))
                        return s;
                }
                return null;
            }

            // Клиенты
            if (id.StartsWith("Client:"))
            {
                var allClients = FindObjectsOfType<ClientPathfinding>();
                if (int.TryParse(id.Substring(7), out int idx) && idx >= 0 && idx < allClients.Length)
                    return allClients[idx];
                return null;
            }

            // Произвольный ключ
            if (characters.TryGetValue(id, out var cached))
                return cached;

            return null;
        }

        /// <summary>
        /// Получить персонажа как GameObject.
        /// </summary>
        public GameObject GetCharacterGameObject(string id)
        {
            var character = GetCharacter(id);
            if (character == null) return null;
            return character.gameObject;
        }

        /// <summary>
        /// Получить компонент персонажа.
        /// </summary>
        public T GetCharacterComponent<T>(string id) where T : Component
        {
            var character = GetCharacter(id);
            if (character == null) return null;
            
            if (character is T comp) return comp;
            return character.GetComponent<T>();
        }

        /// <summary>
        /// Зарегистрировать персонажа под указанным ключом.
        /// </summary>
        public void Register(string key, MonoBehaviour character)
        {
            if (characters.ContainsKey(key))
                characters[key] = character;
            else
                characters.Add(key, character);
        }

        /// <summary>
        /// Удалить регистрацию персонажа.
        /// </summary>
        public void Unregister(string key)
        {
            if (characters.ContainsKey(key))
                characters.Remove(key);
        }
    }
}
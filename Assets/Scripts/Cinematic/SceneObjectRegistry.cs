// === FILE: Assets/Scripts/Cinematic/SceneObjectRegistry.cs ===
using System.Collections.Generic;
using UnityEngine;

namespace CinematicSystem
{
    /// <summary>
    /// Реестр объектов сцены для доступа по строковым ключам.
    /// Используется узлами для поиска точек, UI элементов и других объектов.
    /// </summary>
    public class SceneObjectRegistry : MonoBehaviour
    {
        /// <summary>Singleton instance</summary>
        public static SceneObjectRegistry Instance { get; private set; }

        /// <summary>Запись реестра: ключ -> объект</summary>
        [System.Serializable]
        public class SceneObjectEntry
        {
            public string key;
            public UnityEngine.Object target;
        }

        /// <summary>Список записей для отображения в инспекторе</summary>
        public List<SceneObjectEntry> entries = new List<SceneObjectEntry>();

        /// <summary>Внутренний словарь для быстрого доступа</summary>
        private Dictionary<string, UnityEngine.Object> dict = new Dictionary<string, UnityEngine.Object>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildDictionary();
        }

        /// <summary>
        /// Перестраивает внутренний словарь из списка записей.
        /// </summary>
        private void BuildDictionary()
        {
            dict.Clear();
            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.key) && entry.target != null)
                    dict[entry.key] = entry.target;
            }
        }

        /// <summary>
        /// Получить Transform объекта по ключу.
        /// </summary>
        public Transform GetTransform(string key)
        {
            if (dict.TryGetValue(key, out var obj) && obj is GameObject go)
                return go.transform;
            if (dict.TryGetValue(key, out var comp) && comp is Component c)
                return c.transform;
            return null;
        }

        /// <summary>
        /// Получить GameObject по ключу.
        /// </summary>
        public GameObject GetGameObject(string key)
        {
            if (dict.TryGetValue(key, out var obj) && obj is GameObject go)
                return go;
            if (dict.TryGetValue(key, out var comp) && comp is Component c)
                return c.gameObject;
            return null;
        }

        /// <summary>
        /// Получить компонент указанного типа у объекта по ключу.
        /// </summary>
        public T GetComponent<T>(string key) where T : Component
        {
            var go = GetGameObject(key);
            if (go != null) return go.GetComponent<T>();
            return null;
        }

        /// <summary>
        /// Зарегистрировать объект под указанным ключом.
        /// </summary>
        public void Register(string key, UnityEngine.Object obj)
        {
            if (dict.ContainsKey(key))
                dict[key] = obj;
            else
                dict.Add(key, obj);
            
            // Обновляем список для инспектора
            var entry = entries.Find(e => e.key == key);
            if (entry != null) 
                entry.target = obj;
            else 
                entries.Add(new SceneObjectEntry { key = key, target = obj });
        }

        /// <summary>
        /// Удалить регистрацию по ключу.
        /// </summary>
        public void Unregister(string key)
        {
            dict.Remove(key);
            entries.RemoveAll(e => e.key == key);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Автозаполнение: ищет объекты на сцене и добавляет их в реестр.
        /// Вызывается из контекстного меню в инспекторе.
        /// </summary>
        [ContextMenu("Auto-fill from scene")]
        private void AutoFillFromScene()
        {
            entries.Clear();
            dict.Clear();
            
            var objects = FindObjectsOfType<GameObject>();
            foreach (var go in objects)
            {
                var entry = new SceneObjectEntry { key = go.name, target = go };
                entries.Add(entry);
                dict[go.name] = go;
            }
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[SceneObjectRegistry] Auto-filled with {objects.Length} objects");
        }
#endif
    }
}
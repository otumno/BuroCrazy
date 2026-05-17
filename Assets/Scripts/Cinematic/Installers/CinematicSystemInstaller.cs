// === FILE: Assets/Scripts/Cinematic/Installers/CinematicSystemInstaller.cs ===
using UnityEngine;
using Zenject;
using Managers;

namespace CinematicSystem.Installers
{
    /// <summary>
    /// Zenject installer для кинематической системы.
    /// Регистрирует все сервисы системы в DI контейнере.
    /// </summary>
    [System.Serializable]
    public class CinematicSystemInstaller : MonoInstaller, IInstaller
    {
        [Header("Player Settings")]
        [SerializeField]
        private GameObject cinematicPlayerPrefab;
        
        [SerializeField]
        private bool autoCreatePlayer = true;
        
        [SerializeField]
        private Transform playerParent;

        public override void InstallBindings()
        {
            // Менеджер триггеров - создаём новый на сцене
            Container.Bind<CinematicTriggerManager>()
                .FromNewComponentOnNewGameObject()
                .AsSingle()
                .NonLazy();
            
            // CinematicPlayer на сцене или создаём новый
            var existingPlayer = UnityEngine.Object.FindObjectOfType<CinematicPlayer>();
            if (existingPlayer != null)
            {
                Container.Bind<CinematicPlayer>()
                    .FromInstance(existingPlayer)
                    .AsSingle();
            }
            else if (autoCreatePlayer)
            {
                var playerObject = CreateCinematicPlayer();
                var player = playerObject.GetComponent<CinematicPlayer>();
                Container.Bind<CinematicPlayer>()
                    .FromInstance(player)
                    .AsSingle();
            }
        }

        private GameObject CreateCinematicPlayer()
        {
            GameObject playerObject;
            
            if (cinematicPlayerPrefab != null)
            {
                playerObject = Container.InstantiatePrefab(cinematicPlayerPrefab);
            }
            else
            {
                playerObject = new GameObject("CinematicPlayer");
                
                if (playerParent != null)
                {
                    playerObject.transform.SetParent(playerParent);
                }
                
                playerObject.AddComponent<CinematicPlayer>();
            }
            
            return playerObject;
        }
    }

    /// <summary>
    /// Менеджер триггеров кинематических графов.
    /// Централизует логику регистрации и запуска триггеров.
    /// </summary>
    public class CinematicTriggerManager
    {
        private readonly System.Collections.Generic.List<CinematicTrigger> triggers = 
            new System.Collections.Generic.List<CinematicTrigger>();

        /// <summary>
        /// Зарегистрировать триггер.
        /// </summary>
        public void Register(CinematicTrigger trigger)
        {
            if (trigger != null && !triggers.Contains(trigger))
            {
                triggers.Add(trigger);
            }
        }

        /// <summary>
        /// Удалить триггер из реестра.
        /// </summary>
        public void Unregister(CinematicTrigger trigger)
        {
            triggers.Remove(trigger);
        }

        /// <summary>
        /// Запустить триггер по имени.
        /// </summary>
        public void Trigger(string triggerName)
        {
            var trigger = triggers.Find(t => t != null && t.name == triggerName);
            if (trigger != null)
            {
                trigger.Trigger();
            }
        }

        /// <summary>
        /// Сбросить все триггеры.
        /// </summary>
        public void ResetAll()
        {
            foreach (var trigger in triggers)
            {
                if (trigger != null)
                {
                    trigger.ResetTrigger();
                }
            }
        }

        /// <summary>
        /// Количество активных триггеров.
        /// </summary>
        public int Count => triggers.Count;
    }
}
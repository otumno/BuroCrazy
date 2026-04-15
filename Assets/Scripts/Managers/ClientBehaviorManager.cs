using UnityEngine;
using AI.BehaviorTree.Client;

namespace Managers
{
    /// <summary>
    /// Режим работы клиента
    /// </summary>
    public enum ClientBehaviorMode
    {
        /// <summary>Только State Machine (текущая система)</summary>
        StateMachineOnly,
        /// <summary>Только Behavior Tree (новая система)</summary>
        BehaviorTreeOnly,
        /// <summary>Гибрид: State Machine для действий, BT для решений</summary>
        Hybrid
    }

    /// <summary>
    /// Интеграция Behavior Tree с ClientStateMachine
    /// </summary>
    [RequireComponent(typeof(ClientStateMachine))]
    [RequireComponent(typeof(ClientPathfinding))]
    public class ClientBehaviorManager : MonoBehaviour
    {
        [Header("Режим работы")]
        [SerializeField] private ClientBehaviorMode behaviorMode = ClientBehaviorMode.StateMachineOnly;

        [Header("Behavior Tree настройки")]
        [SerializeField] private bool useBehaviorTree = false;
        [SerializeField] private float btTickInterval = 0.1f;

        [Header("Отладка")]
        [SerializeField] private bool debugMode = false;
        [SerializeField] private bool enableFallbackToSM = true;

        private ClientPathfinding clientPathfinding;
        private ClientStateMachine stateMachine;
        private ClientBehaviorTree behaviorTree;

        public ClientBehaviorMode BehaviorMode
        {
            get => behaviorMode;
            set
            {
                if (value != behaviorMode)
                {
                    SwitchMode(value);
                }
            }
        }

        public bool IsUsingBehaviorTree => behaviorTree != null && behaviorTree.IsRunning;

        private void Awake()
        {
            // ПРИНУДИТЕЛЬНО ОТКЛЮЧАЕМ BT ДЛЯ ПРЕДОТВРАЩЕНИЯ КОНФЛИКТОВ
            useBehaviorTree = false;
            behaviorMode = ClientBehaviorMode.StateMachineOnly;

            clientPathfinding = GetComponent<ClientPathfinding>();
            stateMachine = GetComponent<ClientStateMachine>();

            if (useBehaviorTree && behaviorMode == ClientBehaviorMode.BehaviorTreeOnly)
            {
                InitializeBehaviorTree();
            }
        }

        private void Start()
        {
            if (useBehaviorTree && behaviorMode == ClientBehaviorMode.BehaviorTreeOnly)
            {
                StartBehaviorTree();
            }
        }

        private void Update()
        {
            if (useBehaviorTree && behaviorTree != null && behaviorTree.IsRunning)
            {
                behaviorTree.Update();

                // Fallback если BT завис
                if (enableFallbackToSM && IsBehaviorTreeStuck())
                {
                    Debug.LogWarning($"[ClientBehaviorManager] {name}: BT завис, переключаемся на SM");
                    SwitchToStateMachine();
                }
            }
        }

        /// <summary>
        /// Инициализировать Behavior Tree
        /// </summary>
        public void InitializeBehaviorTree()
        {
            if (clientPathfinding == null)
            {
                Debug.LogError("[ClientBehaviorManager] ClientPathfinding is null!");
                return;
            }

            behaviorTree = new ClientBehaviorTree(clientPathfinding, btTickInterval);

            if (debugMode)
            {
                Debug.Log($"[ClientBehaviorManager] {name}: BT initialized");
            }
        }

        /// <summary>
        /// Запустить Behavior Tree
        /// </summary>
        public void StartBehaviorTree()
        {
            if (behaviorTree == null)
            {
                InitializeBehaviorTree();
            }

            behaviorTree?.Start();

            if (debugMode)
            {
                Debug.Log($"[ClientBehaviorManager] {name}: BT started");
            }
        }

        /// <summary>
        /// Остановить Behavior Tree
        /// </summary>
        public void StopBehaviorTree()
        {
            behaviorTree?.Stop();

            if (debugMode)
            {
                Debug.Log($"[ClientBehaviorManager] {name}: BT stopped");
            }
        }

        /// <summary>
        /// Переключить режим работы
        /// </summary>
        public void SwitchMode(ClientBehaviorMode newMode)
        {
            switch (newMode)
            {
                case ClientBehaviorMode.StateMachineOnly:
                    SwitchToStateMachine();
                    break;
                case ClientBehaviorMode.BehaviorTreeOnly:
                    SwitchToBehaviorTree();
                    break;
                case ClientBehaviorMode.Hybrid:
                    InitializeBehaviorTree();
                    StartBehaviorTree();
                    break;
            }

            behaviorMode = newMode;
        }

        private void SwitchToStateMachine()
        {
            StopBehaviorTree();

            if (stateMachine != null)
            {
                stateMachine.StartCoroutine(stateMachine.MainLogicLoop());
            }

            if (debugMode)
            {
                Debug.Log($"[ClientBehaviorManager] {name}: Switched to State Machine");
            }
        }

        private void SwitchToBehaviorTree()
        {
            // Останавливаем State Machine
            if (stateMachine != null)
            {
                stateMachine.StopAllActionCoroutines();
            }

            InitializeBehaviorTree();
            StartBehaviorTree();

            if (debugMode)
            {
                Debug.Log($"[ClientBehaviorManager] {name}: Switched to Behavior Tree");
            }
        }

        private bool IsBehaviorTreeStuck()
        {
            if (behaviorTree == null) return false;

            // Проверяем, не завис ли клиент в одном состоянии слишком долго
            var sm = stateMachine;
            if (sm == null) return false;

            var currentState = sm.GetCurrentState();

            // Если в MovingToGoal слишком долго (>15 сек) - завис
            if (currentState == ClientState.MovingToGoal)
            {
                // TODO: добавить проверку времени
                return false;
            }

            return false;
        }

        /// <summary>
        /// Переключиться на State Machine (публичный метод)
        /// </summary>
        public void FallbackToStateMachine()
        {
            useBehaviorTree = false;
            SwitchToStateMachine();
        }

        /// <summary>
        /// Получить Behavior Tree для расширенного управления
        /// </summary>
        public ClientBehaviorTree GetBehaviorTree()
        {
            return behaviorTree;
        }

        private void OnDestroy()
        {
            StopBehaviorTree();
        }
    }
}

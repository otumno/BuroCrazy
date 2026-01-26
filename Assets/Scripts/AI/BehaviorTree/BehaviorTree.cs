using UnityEngine;
using System;

namespace AI.BehaviorTree
{
    /// <summary>
    /// Менеджер Behavior Tree для клиента
    /// </summary>
    public class BehaviorTree
    {
        private BTNode root;
        private bool isRunning = false;
        private float lastTickTime = 0f;
        private float tickInterval = 0.1f;

        public BTNode Root => root;
        public bool IsRunning => isRunning;
        public float TickInterval
        {
            get => tickInterval;
            set => tickInterval = Mathf.Max(0.01f, value);
        }

        public BehaviorTree(BTNode rootNode, float tickInterval = 0.1f)
        {
            root = rootNode;
            this.tickInterval = tickInterval;
        }

        /// <summary>
        /// Запустить дерево
        /// </summary>
        public void Start()
        {
            if (root == null)
            {
                Debug.LogError("[BehaviorTree] Root node is null!");
                return;
            }

            isRunning = true;
            lastTickTime = Time.time;
            Debug.Log("[BehaviorTree] Started");
        }

        /// <summary>
        /// Остановить дерево
        /// </summary>
        public void Stop()
        {
            isRunning = false;
            root?.Halt();
            Debug.Log("[BehaviorTree] Stopped");
        }

        /// <summary>
        /// Обновить дерево (вызывать в Update)
        /// </summary>
        public void Update()
        {
            if (!isRunning || root == null) return;

            if (Time.time - lastTickTime >= tickInterval)
            {
                lastTickTime = Time.time;
                BTNodeResult result = root.Tick();

                if (result != BTNodeResult.Running)
                {
                    // Дерево завершилось
                    Debug.Log($"[BehaviorTree] Completed with: {result}");
                }
            }
        }

        /// <summary>
        /// Принудительный тик (для немедленной проверки)
        /// </summary>
        public BTNodeResult ForceTick()
        {
            if (root == null) return BTNodeResult.Failure;
            return root.Tick();
        }

        /// <summary>
        /// Сбросить дерево в начальное состояние
        /// </summary>
        public void Reset()
        {
            root?.Reset();
            lastTickTime = Time.time;
        }

        /// <summary>
        /// Прервать выполнение
        /// </summary>
        public void Halt()
        {
            root?.Halt();
        }
    }
}

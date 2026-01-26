using System.Collections.Generic;

namespace AI.BehaviorTree
{
    /// <summary>
    /// Результат выполнения узла Behavior Tree
    /// </summary>
    public enum BTNodeResult
    {
        /// <summary>Узел выполнился успешно</summary>
        Success,
        /// <summary>Узел выполняется (требует продолжения)</summary>
        Running,
        /// <summary>Узел выполнился с ошибкой</summary>
        Failure
    }

    /// <summary>
    /// Базовый класс для всех узлов Behavior Tree
    /// </summary>
    public abstract class BTNode
    {
        protected readonly string name;
        protected BTNode parent;
        protected readonly System.Collections.Generic.List<BTNode> children = new System.Collections.Generic.List<BTNode>();

        public string Name => name;
        public BTNode Parent => parent;
        public System.Collections.Generic.IReadOnlyList<BTNode> Children => children;

        protected BTNode(string name)
        {
            this.name = name;
        }

        /// <summary>
        /// Добавить дочерний узел
        /// </summary>
        public void AddChild(BTNode child)
        {
            if (child == null) return;

            children.Add(child);
            child.parent = this;
        }

        /// <summary>
        /// Оценка узла (вызывается каждый тик)
        /// </summary>
        public abstract BTNodeResult Tick();

        /// <summary>
        /// Сброс состояния узла
        /// </summary>
        public virtual void Reset()
        {
            foreach (var child in children)
            {
                child.Reset();
            }
        }

        /// <summary>
        /// Прерывание выполнения (остановка всех Running потомков)
        /// </summary>
        public virtual void Halt()
        {
            Reset();
        }

        protected void Log(string message)
        {
            // UnityEngine.Debug.Log($"[BT] {name}: {message}");
        }
    }

    /// <summary>
    /// Тип прерывания для Parallel узла
    /// </summary>
    public enum ParallelPolicy
    {
        /// <summary>Успех если все дети успешны</summary>
        AllMustSucceed,
        /// <summary>Успех если хотя бы один ребёнок успешен</summary>
        OneMustSucceed,
        /// <summary>Успех если большинство успешны</summary>
        MajorityMustSucceed
    }
}

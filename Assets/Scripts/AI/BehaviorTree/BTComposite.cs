namespace AI.BehaviorTree
{
    /// <summary>
    /// Composite узел: выполняет детей по очереди (Sequence)
    /// Возвращает Success только если ВСЕ дети успешны
    /// </summary>
    public class BTSequence : BTNode
    {
        private int currentChildIndex = 0;

        public BTSequence(string name) : base(name) { }

        public override BTNodeResult Tick()
        {
            if (children.Count == 0)
            {
                Log("No children, returning Success");
                return BTNodeResult.Success;
            }

            // Если первый тик или возврат после успеха - начинаем сначала
            if (currentChildIndex >= children.Count)
            {
                Log("Sequence completed, returning Success");
                return BTNodeResult.Success;
            }

            BTNodeResult result = children[currentChildIndex].Tick();

            switch (result)
            {
                case BTNodeResult.Success:
                    Log($"{children[currentChildIndex].Name} succeeded, moving to next");
                    currentChildIndex++;
                    if (currentChildIndex >= children.Count)
                    {
                        Log("All children succeeded");
                        return BTNodeResult.Success;
                    }
                    return BTNodeResult.Running;

                case BTNodeResult.Running:
                    Log($"{children[currentChildIndex].Name} is running");
                    return BTNodeResult.Running;

                case BTNodeResult.Failure:
                    Log($"{children[currentChildIndex].Name} failed, sequence failed");
                    currentChildIndex = 0;
                    return BTNodeResult.Failure;

                default:
                    return BTNodeResult.Failure;
            }
        }

        public override void Reset()
        {
            currentChildIndex = 0;
            base.Reset();
        }

        public override void Halt()
        {
            currentChildIndex = 0;
            base.Halt();
        }
    }

    /// <summary>
    /// Composite узел: Selector (выбор)
    /// Пробует детей по очереди пока один не вернёт Success
    /// </summary>
    public class BTSelector : BTNode
    {
        private int currentChildIndex = 0;

        public BTSelector(string name) : base(name) { }

        public override BTNodeResult Tick()
        {
            if (children.Count == 0)
            {
                Log("No children, returning Failure");
                return BTNodeResult.Failure;
            }

            if (currentChildIndex >= children.Count)
            {
                Log("All children failed, returning Failure");
                currentChildIndex = 0;
                return BTNodeResult.Failure;
            }

            BTNodeResult result = children[currentChildIndex].Tick();

            switch (result)
            {
                case BTNodeResult.Success:
                    Log($"{children[currentChildIndex].Name} succeeded, selector succeeded");
                    currentChildIndex = 0;
                    return BTNodeResult.Success;

                case BTNodeResult.Running:
                    Log($"{children[currentChildIndex].Name} is running");
                    return BTNodeResult.Running;

                case BTNodeResult.Failure:
                    Log($"{children[currentChildIndex].Name} failed, trying next");
                    currentChildIndex++;
                    if (currentChildIndex >= children.Count)
                    {
                        Log("All children failed");
                        currentChildIndex = 0;
                        return BTNodeResult.Failure;
                    }
                    return BTNodeResult.Running;

                default:
                    return BTNodeResult.Failure;
            }
        }

        public override void Reset()
        {
            currentChildIndex = 0;
            base.Reset();
        }

        public override void Halt()
        {
            currentChildIndex = 0;
            base.Halt();
        }
    }

    /// <summary>
    /// Composite узел: Parallel (параллельное выполнение)
    /// Выполняет всех детей одновременно
    /// </summary>
    public class BTParallel : BTNode
    {
        private readonly ParallelPolicy policy;
        private int successCount;
        private int failureCount;

        public BTParallel(string name, ParallelPolicy policy = ParallelPolicy.AllMustSucceed) : base(name)
        {
            this.policy = policy;
        }

        public override BTNodeResult Tick()
        {
            if (children.Count == 0)
            {
                return BTNodeResult.Success;
            }

            successCount = 0;
            failureCount = 0;
            int runningCount = 0;

            foreach (var child in children)
            {
                BTNodeResult result = child.Tick();

                switch (result)
                {
                    case BTNodeResult.Success:
                        successCount++;
                        break;
                    case BTNodeResult.Failure:
                        failureCount++;
                        break;
                    case BTNodeResult.Running:
                        runningCount++;
                        break;
                }
            }

            switch (policy)
            {
                case ParallelPolicy.AllMustSucceed:
                    if (successCount == children.Count)
                    {
                        Log("All children succeeded");
                        return BTNodeResult.Success;
                    }
                    if (failureCount > 0)
                    {
                        Log("Some children failed");
                        return BTNodeResult.Failure;
                    }
                    return BTNodeResult.Running;

                case ParallelPolicy.OneMustSucceed:
                    if (successCount > 0)
                    {
                        Log("At least one child succeeded");
                        return BTNodeResult.Success;
                    }
                    if (failureCount == children.Count)
                    {
                        Log("All children failed");
                        return BTNodeResult.Failure;
                    }
                    return BTNodeResult.Running;

                case ParallelPolicy.MajorityMustSucceed:
                    int majority = (children.Count / 2) + 1;
                    if (successCount >= majority)
                    {
                        Log("Majority succeeded");
                        return BTNodeResult.Success;
                    }
                    if (failureCount >= majority)
                    {
                        Log("Majority failed");
                        return BTNodeResult.Failure;
                    }
                    return BTNodeResult.Running;

                default:
                    return BTNodeResult.Failure;
            }
        }

        public override void Reset()
        {
            successCount = 0;
            failureCount = 0;
            base.Reset();
        }
    }
}

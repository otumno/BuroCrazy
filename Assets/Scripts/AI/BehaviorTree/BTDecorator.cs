using UnityEngine;

namespace AI.BehaviorTree
{
    /// <summary>
    /// Decorator узел: Invert (инвертирует результат)
    /// Success → Failure, Failure → Success, Running → Running
    /// </summary>
    public class BTInvert : BTNode
    {
        private BTNode child;

        public BTInvert(string name, BTNode child) : base(name)
        {
            this.child = child;
        }

        public override BTNodeResult Tick()
        {
            if (child == null)
            {
                Log("No child, returning Failure");
                return BTNodeResult.Failure;
            }

            BTNodeResult result = child.Tick();

            switch (result)
            {
                case BTNodeResult.Success:
                    Log("Success → Failure");
                    return BTNodeResult.Failure;
                case BTNodeResult.Failure:
                    Log("Failure → Success");
                    return BTNodeResult.Success;
                case BTNodeResult.Running:
                    return BTNodeResult.Running;
                default:
                    return BTNodeResult.Failure;
            }
        }

        public override void Reset()
        {
            child?.Reset();
            base.Reset();
        }

        public override void Halt()
        {
            child?.Halt();
            base.Halt();
        }
    }

    /// <summary>
    /// Decorator узел: Repeat (повторяет ребёнка N раз)
    /// </summary>
    public class BTRepeat : BTNode
    {
        private readonly int count;
        private readonly bool infinite;
        private int currentCount;
        private BTNode child;

        public BTRepeat(string name, int repeatCount, BTNode child) : base(name)
        {
            this.count = repeatCount;
            this.infinite = false;
            this.child = child;
            this.currentCount = 0;
        }

        public BTRepeat(string name, BTNode child) : base(name)
        {
            this.count = -1;
            this.infinite = true;
            this.child = child;
            this.currentCount = 0;
        }

        public override BTNodeResult Tick()
        {
            if (child == null) return BTNodeResult.Failure;

            BTNodeResult result = child.Tick();

            if (result == BTNodeResult.Running)
            {
                return BTNodeResult.Running;
            }

            if (result == BTNodeResult.Success)
            {
                currentCount++;

                if (!infinite && currentCount >= count)
                {
                    Log($"Completed {count} iterations, returning Success");
                    currentCount = 0;
                    child.Reset();
                    return BTNodeResult.Success;
                }

                // Продолжаем повторять
                child.Reset();
                return BTNodeResult.Running;
            }

            // Failure - останавливаем
            Log("Child failed, stopping repeat");
            currentCount = 0;
            return BTNodeResult.Failure;
        }

        public override void Reset()
        {
            currentCount = 0;
            child?.Reset();
            base.Reset();
        }
    }

    /// <summary>
    /// Decorator узел: Timeout (прерывает если слишком долго)
    /// </summary>
    public class BTTimeout : BTNode
    {
        private readonly float timeoutDuration;
        private float elapsedTime;
        private BTNode child;

        public BTTimeout(string name, float timeoutSeconds, BTNode child) : base(name)
        {
            this.timeoutDuration = timeoutSeconds;
            this.child = child;
            this.elapsedTime = 0f;
        }

        public override BTNodeResult Tick()
        {
            if (child == null) return BTNodeResult.Failure;

            BTNodeResult result = child.Tick();

            if (result == BTNodeResult.Running)
            {
                elapsedTime += Time.deltaTime;
                if (elapsedTime >= timeoutDuration)
                {
                    Log($"Timeout reached ({timeoutDuration}s), returning Failure");
                    child.Halt();
                    elapsedTime = 0f;
                    return BTNodeResult.Failure;
                }
                return BTNodeResult.Running;
            }

            // Success или Failure - сбрасываем таймер
            elapsedTime = 0f;
            return result;
        }

        public override void Reset()
        {
            elapsedTime = 0f;
            child?.Reset();
            base.Reset();
        }

        public override void Halt()
        {
            elapsedTime = 0f;
            child?.Halt();
            base.Halt();
        }
    }

    /// <summary>
    /// Decorator узел: Cooldown (задержка между выполнениями)
    /// </summary>
    public class BTCooldown : BTNode
    {
        private readonly float cooldownDuration;
        private float cooldownTimer;
        private BTNode child;

        public BTCooldown(string name, float cooldownSeconds, BTNode child) : base(name)
        {
            this.cooldownDuration = cooldownSeconds;
            this.child = child;
            this.cooldownTimer = 0f;
        }

        public override BTNodeResult Tick()
        {
            if (child == null) return BTNodeResult.Failure;

            // Если на кулдауне - сразу Failure
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
                return BTNodeResult.Failure;
            }

            BTNodeResult result = child.Tick();

            if (result == BTNodeResult.Running)
            {
                return BTNodeResult.Running;
            }

            // После выполнения запускаем кулдаун
            if (result == BTNodeResult.Success)
            {
                cooldownTimer = cooldownDuration;
                Log($"Cooldown started: {cooldownDuration}s");
            }

            return result;
        }

        public override void Reset()
        {
            cooldownTimer = 0f;
            child?.Reset();
            base.Reset();
        }
    }

    /// <summary>
    /// Decorator узел: Condition (проверяет условие)
    /// Выполняет ребёнка только если условие верно
    /// </summary>
    public class BTCondition : BTNode
    {
        private readonly System.Func<bool> condition;
        private readonly bool negate;
        private BTNode child;

        public BTCondition(string name, System.Func<bool> condition, BTNode child = null, bool negate = false) : base(name)
        {
            this.condition = condition;
            this.negate = negate;
            this.child = child;
        }

        public override BTNodeResult Tick()
        {
            if (condition == null)
            {
                Log("No condition, returning Failure");
                return BTNodeResult.Failure;
            }

            bool result = condition();
            bool finalResult = negate ? !result : result;

            Log($"Condition: {result}, Negate: {negate}, Final: {finalResult}");

            if (!finalResult)
            {
                // Условие не выполнено - Failure (или Success если negated)
                return negate ? BTNodeResult.Success : BTNodeResult.Failure;
            }

            // Условие выполнено - выполняем ребёнка
            if (child != null)
            {
                return child.Tick();
            }

            return BTNodeResult.Success;
        }

        public override void Reset()
        {
            child?.Reset();
            base.Reset();
        }

        public override void Halt()
        {
            child?.Halt();
            base.Halt();
        }
    }

    /// <summary>
    /// Decorator узел: UntilFail (повторяет пока не Failure)
    /// </summary>
    public class BTUntilFail : BTNode
    {
        private BTNode child;

        public BTUntilFail(string name, BTNode child) : base(name)
        {
            this.child = child;
        }

        public override BTNodeResult Tick()
        {
            if (child == null) return BTNodeResult.Success;

            BTNodeResult result = child.Tick();

            if (result == BTNodeResult.Running)
            {
                return BTNodeResult.Running;
            }

            if (result == BTNodeResult.Success)
            {
                // Успех - повторяем
                child.Reset();
                return BTNodeResult.Running;
            }

            // Failure - останавливаем
            Log("Child failed, stopping");
            return BTNodeResult.Success;
        }

        public override void Reset()
        {
            child?.Reset();
            base.Reset();
        }
    }
}

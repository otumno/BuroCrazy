using UnityEngine;
using Managers;
using Enums;

namespace AI.BehaviorTree.Client
{
    /// <summary>
    /// Уровень накопления стресса/терпения клиента
    /// </summary>
    public enum HeatLevel
    {
        Calm,        // 0-40% терпения
        Grumbling,   // 40-70% терпения
        Frustrated,  // 70-90% терпения
        Enraged      // 90-100% терпения
    }

    /// <summary>
    /// Базовый класс для действий клиента
    /// </summary>
    public abstract class ClientLeaf : BTNode
    {
        protected readonly ClientPathfinding client;

        protected ClientLeaf(string name, ClientPathfinding client) : base(name)
        {
            this.client = client;
        }

        protected ClientStateMachine GetStateMachine()
        {
            return client?.stateMachine;
        }

        protected bool IsInState(params ClientState[] states)
        {
            var sm = GetStateMachine();
            if (sm == null) return false;

            var current = sm.GetCurrentState();
            foreach (var state in states)
            {
                if (current == state) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Leaf: Переход в состояние
    /// </summary>
    public class SetStateNode : ClientLeaf
    {
        private readonly ClientState targetState;

        public SetStateNode(string name, ClientPathfinding client, ClientState state) : base(name, client)
        {
            targetState = state;
        }

        public override BTNodeResult Tick()
        {
            var sm = GetStateMachine();
            if (sm == null) return BTNodeResult.Failure;

            sm.SetState(targetState);
            Log($"Set state to {targetState}");
            return BTNodeResult.Success;
        }
    }

    /// <summary>
    /// Leaf: Установить цель
    /// </summary>
    public class SetGoalNode : ClientLeaf
    {
        private readonly System.Func<Waypoint> goalProvider;

        public SetGoalNode(string name, ClientPathfinding client, System.Func<Waypoint> goalProvider) : base(name, client)
        {
            this.goalProvider = goalProvider;
        }

        public override BTNodeResult Tick()
        {
            var goal = goalProvider?.Invoke();
            if (goal == null)
            {
                Log("Goal is null, returning Failure");
                return BTNodeResult.Failure;
            }

            GetStateMachine()?.SetGoal(goal);
            Log($"Set goal to {goal.name}");
            return BTNodeResult.Success;
        }
    }

    /// <summary>
    /// Leaf: Идти к цели (пока не дойдёт)
    /// </summary>
    public class MoveToGoalNode : ClientLeaf
    {
        public MoveToGoalNode(string name, ClientPathfinding client) : base(name, client) { }

        public override BTNodeResult Tick()
        {
            var sm = GetStateMachine();
            if (sm == null) return BTNodeResult.Failure;

            var currentState = sm.GetCurrentState();

            // Если уже идёт к цели - Running
            if (currentState == ClientState.MovingToGoal ||
                currentState == ClientState.MovingToRegistrarImpolite ||
                currentState == ClientState.ReturningToRegistrar ||
                currentState == ClientState.GoingToCashier ||
                currentState == ClientState.ReturningToWait ||
                currentState == ClientState.MovingToSeat)
            {
                return BTNodeResult.Running;
            }

            // Если прибыл - Success
            if (currentState == ClientState.AtRegistration ||
                currentState == ClientState.AtCashier ||
                currentState == ClientState.AtDesk1 ||
                currentState == ClientState.AtDesk2 ||
                currentState == ClientState.InsideLimitedZone ||
                currentState == ClientState.SittingInWaitingArea ||
                currentState == ClientState.AtWaitingArea)
            {
                Log("Already at goal");
                return BTNodeResult.Success;
            }

            // Иначе - запускаем движение
            sm.SetState(ClientState.MovingToGoal);
            Log("Started moving to goal");
            return BTNodeResult.Running;
        }
    }

    /// <summary>
    /// Leaf: Ворчать (Grumbling)
    /// </summary>
    public class GrumbleNode : ClientLeaf
    {
        private readonly string message;
        private bool hasGrumbled = false;

        public GrumbleNode(string name, ClientPathfinding client, string message = null) : base(name, client)
        {
            this.message = message;
        }

        public override BTNodeResult Tick()
        {
            var sm = GetStateMachine();
            if (sm == null) return BTNodeResult.Failure;

            // Переходим в состояние ворчания
            if (!hasGrumbled)
            {
                sm.SetState(ClientState.Grumbling);
                hasGrumbled = true;
                Log("Started grumbling");
            }

            // Если всё ещё ворчим - Running
            if (sm.GetCurrentState() == ClientState.Grumbling)
            {
                return BTNodeResult.Running;
            }

            // Вышли из ворчания - Success
            hasGrumbled = false;
            Log("Finished grumbling");
            return BTNodeResult.Success;
        }

        public override void Reset()
        {
            hasGrumbled = false;
            base.Reset();
        }
    }

    /// <summary>
    /// Leaf: Проверить уровень терпения
    /// </summary>
    public class CheckHeatLevelNode : ClientLeaf
    {
        private readonly HeatLevel level;
        private readonly System.Func<HeatLevel> heatProvider;

        public CheckHeatLevelNode(string name, ClientPathfinding client, HeatLevel level) : base(name, client)
        {
            this.level = level;
            this.heatProvider = () => GetHeatLevel(client);
        }

        public CheckHeatLevelNode(string name, System.Func<HeatLevel> heatProvider) : base(name, null)
        {
            this.level = HeatLevel.Calm;
            this.heatProvider = heatProvider;
        }

        public override BTNodeResult Tick()
        {
            HeatLevel current = heatProvider?.Invoke() ?? HeatLevel.Calm;
            Log($"Current heat: {current}, Checking: {level}");

            return current == level ? BTNodeResult.Success : BTNodeResult.Failure;
        }

        public static HeatLevel GetHeatLevel(ClientPathfinding client)
        {
            if (client == null) return HeatLevel.Calm;

            float heat = client.PatienceHeat;

            if (heat >= 0.9f) return HeatLevel.Enraged;
            if (heat >= 0.7f) return HeatLevel.Frustrated;
            if (heat >= 0.4f) return HeatLevel.Grumbling;
            return HeatLevel.Calm;
        }
    }

    /// <summary>
    /// Leaf: Проверить условие из функции
    /// </summary>
    public class ClientConditionNode : ClientLeaf
    {
        private readonly System.Func<bool> condition;

        public ClientConditionNode(string name, ClientPathfinding client, System.Func<bool> condition) : base(name, client)
        {
            this.condition = condition;
        }

        public override BTNodeResult Tick()
        {
            bool result = condition?.Invoke() ?? false;
            Log($"Condition: {result}");
            return result ? BTNodeResult.Success : BTNodeResult.Failure;
        }
    }

    /// <summary>
    /// Leaf: Уйти (любой исход)
    /// </summary>
    public class LeaveNode : ClientLeaf
    {
        private readonly bool isUpset;

        public LeaveNode(string name, ClientPathfinding client, bool isUpset = false) : base(name, client)
        {
            this.isUpset = isUpset;
        }

        public override BTNodeResult Tick()
        {
            var sm = GetStateMachine();
            if (sm == null) return BTNodeResult.Failure;

            var currentState = sm.GetCurrentState();

            // Если уже уходит - Success
            if (currentState == ClientState.Leaving || currentState == ClientState.LeavingUpset)
            {
                return BTNodeResult.Running;
            }

            // Запускаем уход
            client.reasonForLeaving = isUpset ? ClientPathfinding.LeaveReason.Upset : ClientPathfinding.LeaveReason.Processed;
            client.isLeavingSuccessfully = !isUpset;

            sm.SetGoal(ClientSpawner.Instance.exitWaypoint);
            sm.SetState(isUpset ? ClientState.LeavingUpset : ClientState.Leaving);

            Log($"Leaving: {(isUpset ? "Upset" : "Success")}");
            return BTNodeResult.Running;
        }
    }

    /// <summary>
    /// Leaf: Переход в ярость
    /// </summary>
    public class EnrageNode : ClientLeaf
    {
        public EnrageNode(string name, ClientPathfinding client) : base(name, client) { }

        public override BTNodeResult Tick()
        {
            var sm = GetStateMachine();
            if (sm == null) return BTNodeResult.Failure;

            if (sm.GetCurrentState() == ClientState.Enraged)
            {
                return BTNodeResult.Running;
            }

            sm.SetState(ClientState.Enraged);
            Log("Enraged!");
            return BTNodeResult.Running;
        }
    }

    /// <summary>
    /// Leaf: Присоединиться к очереди
    /// </summary>
    public class JoinQueueNode : ClientLeaf
    {
        public JoinQueueNode(string name, ClientPathfinding client) : base(name, client) { }

        public override BTNodeResult Tick()
        {
            if (ClientQueueManager.Instance == null) return BTNodeResult.Failure;

            var sm = GetStateMachine();
            if (sm == null) return BTNodeResult.Failure;

            // Проверяем, уже ли в очереди
            if (sm.GetCurrentState() == ClientState.SittingInWaitingArea ||
                sm.GetCurrentState() == ClientState.AtWaitingArea)
            {
                return BTNodeResult.Success;
            }

            // Присоединяемся
            ClientQueueManager.Instance.JoinQueue(client);
            Log("Joined queue");
            return BTNodeResult.Success;
        }
    }

    /// <summary>
    /// Leaf: Подождать (задержка)
    /// </summary>
    public class WaitNode : ClientLeaf
    {
        private readonly float duration;
        private float elapsed;

        public WaitNode(string name, ClientPathfinding client, float duration) : base(name, client)
        {
            this.duration = duration;
            this.elapsed = 0f;
        }

        public override BTNodeResult Tick()
        {
            elapsed += Time.deltaTime;

            if (elapsed >= duration)
            {
                Log($"Waited {duration}s, completed");
                elapsed = 0f;
                return BTNodeResult.Success;
            }

            Log($"Waiting: {elapsed:F1}/{duration:F1}s");
            return BTNodeResult.Running;
        }

        public override void Reset()
        {
            elapsed = 0f;
            base.Reset();
        }
    }
}

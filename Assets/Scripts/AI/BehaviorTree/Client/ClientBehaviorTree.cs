using UnityEngine;
using AI.BehaviorTree;
using Enums;
using Managers;

namespace AI.BehaviorTree.Client
{
    /// <summary>
    /// Фабрика для создания Behavior Tree для клиента
    /// </summary>
    public static class ClientBehaviorTreeFactory
    {
        /// <summary>
        /// Создать Behavior Tree для клиента
        /// </summary>
        public static BehaviorTree Create(ClientPathfinding client, float tickInterval = 0.1f)
        {
            var root = CreateRootNode(client);
            return new BehaviorTree(root, tickInterval);
        }

        private static BTNode CreateRootNode(ClientPathfinding client)
        {
            // Главный Selector - пробуем стратегии по очереди
            var root = new BTSelector("ClientRoot");

            // 1. Попытка выполнить главную цель
            root.AddChild(CreateMainGoalSequence(client));

            // 2. Альтернативный путь (если главный заблокирован)
            root.AddChild(CreateAlternativePathSequence(client));

            // 3. Управление недовольством (Grumbling → Enraged)
            root.AddChild(CreateGrumblingSequence(client));

            // 4. Завершение (уход)
            root.AddChild(CreateLeaveSequence(client));

            return root;
        }

        /// <summary>
        /// Главная цель: зарегистрироваться → получить услугу → оплатить → уйти
        /// </summary>
        private static BTSequence CreateMainGoalSequence(ClientPathfinding client)
        {
            var sequence = new BTSequence("MainGoal");

            // Идём к регистратуре
            sequence.AddChild(new SetGoalNode("SetRegistrationGoal", client,
                () => ClientQueueManager.Instance.ChooseNewGoal(client)));
            sequence.AddChild(new MoveToGoalNode("MoveToRegistration", client));

            // Ждём в очереди (с таймаутом)
            sequence.AddChild(new JoinQueueNode("JoinQueue", client));
            sequence.AddChild(new WaitNode("WaitInQueue", client, 30f));

            // Получаем услугу (переход к следующему состоянию)
            sequence.AddChild(new SetStateNode("GetServed", client, ClientState.AtRegistration));

            // Если нужна касса - идём к кассе
            sequence.AddChild(new ClientConditionNode("NeedCash?", client,
                () => client.billToPay > 0));
            sequence.AddChild(new SetGoalNode("SetCashierGoal", client,
                () => ClientSpawner.GetCashierZone()?.waitingWaypoint));
            sequence.AddChild(new MoveToGoalNode("MoveToCashier", client));
            sequence.AddChild(new SetStateNode("AtCashier", client, ClientState.AtCashier));

            // Уходим довольным
            sequence.AddChild(new LeaveNode("LeaveSuccess", client, isUpset: false));

            return sequence;
        }

        /// <summary>
        /// Альтернативный путь: если очередь переполнена или не могут обслужить
        /// </summary>
        private static BTSequence CreateAlternativePathSequence(ClientPathfinding client)
        {
            var sequence = new BTSequence("AlternativePath");

            // Если очередь переполнена - пробуем пролезть или к директору
            sequence.AddChild(new ClientConditionNode("QueueTooLong?", client,
                () => ClientQueueManager.Instance != null &&
                      ClientQueueManager.Instance.queue.Count > 5));

            // Пытаемся пролезть
            sequence.AddChild(new SetGoalNode("CutInLineGoal", client,
                () => ClientSpawner.GetRegistrationZone()?.waitingWaypoint));
            sequence.AddChild(new MoveToGoalNode("CutInLine", client));

            // Если не удалось - жалуемся директору
            sequence.AddChild(new SetGoalNode("DirectorComplaintGoal", client,
                () => ClientSpawner.Instance?.exitWaypoint));
            sequence.AddChild(new MoveToGoalNode("GoToDirector", client));

            // Уходим расстроенным
            sequence.AddChild(new LeaveNode("LeaveUpset", client, isUpset: true));

            return sequence;
        }

        /// <summary>
        /// Управление недовольством: Grumbling → Frustrated → Enraged
        /// </summary>
        private static BTSequence CreateGrumblingSequence(ClientPathfinding client)
        {
            var sequence = new BTSequence("GrumblingBehavior");

            // Проверяем уровень терпения и ворчим если надо
            sequence.AddChild(new ClientConditionNode("IsGrumbling?", client,
                () => client.PatienceHeat >= client.GetEffectiveGrumblingThreshold() &&
                      client.PatienceHeat < 1.0f));

            var grumbleSequence = new BTSequence("GrumbleSequence");
            grumbleSequence.AddChild(new SetStateNode("EnterGrumbling", client, ClientState.Grumbling));
            grumbleSequence.AddChild(new WaitNode("GrumbleDuration", client, 5f));
            grumbleSequence.AddChild(new SetStateNode("ExitGrumbling", client, ClientState.MovingToGoal));
            sequence.AddChild(grumbleSequence);

            // Если терпение на исходе - эскалация
            sequence.AddChild(new ClientConditionNode("PatienceZero?", client,
                () => client.PatienceHeat >= 1.0f));

            var escalateSequence = new BTSequence("Escalate");
            escalateSequence.AddChild(new ClientConditionNode("IsSuetun?", client,
                () => client.suetunFactor > 0.5f));
            escalateSequence.AddChild(new EnrageNode("Enrage", client));
            sequence.AddChild(new LeaveNode("LeaveFrustrated", client, isUpset: true));

            return sequence;
        }

        /// <summary>
        /// Завершение: уход по любой причине
        /// </summary>
        private static BTSequence CreateLeaveSequence(ClientPathfinding client)
        {
            var sequence = new BTSequence("LeaveBehavior");

            // Проверяем, пора ли уходить
            sequence.AddChild(new ClientConditionNode("ShouldLeave?", client,
                () => client.isLeavingSuccessfully ||
                      client.reasonForLeaving != ClientPathfinding.LeaveReason.Normal));

            var leaveSequence = new BTSequence("ExecuteLeave");
            leaveSequence.AddChild(new SetGoalNode("SetExitGoal", client,
                () => ClientSpawner.Instance?.exitWaypoint));
            leaveSequence.AddChild(new MoveToGoalNode("MoveToExit", client));
            leaveSequence.AddChild(new SetStateNode("Leaving", client, ClientState.Leaving));
            sequence.AddChild(leaveSequence);

            return sequence;
        }
    }

    /// <summary>
    /// Класс для управления Behavior Tree клиента
    /// </summary>
    public class ClientBehaviorTree
    {
        private readonly BehaviorTree tree;
        private readonly ClientPathfinding client;

        public bool IsRunning => tree?.IsRunning ?? false;

        public ClientBehaviorTree(ClientPathfinding client, float tickInterval = 0.1f)
        {
            this.client = client;
            this.tree = ClientBehaviorTreeFactory.Create(client, tickInterval);
        }

        /// <summary>
        /// Запустить дерево
        /// </summary>
        public void Start()
        {
            tree?.Start();
        }

        /// <summary>
        /// Остановить дерево
        /// </summary>
        public void Stop()
        {
            tree?.Stop();
        }

        /// <summary>
        /// Обновить дерево (вызывать в Update клиента)
        /// </summary>
        public void Update()
        {
            tree?.Update();
        }

        /// <summary>
        /// Сбросить дерево
        /// </summary>
        public void Reset()
        {
            tree?.Reset();
        }

        /// <summary>
        /// Прервать выполнение
        /// </summary>
        public void Halt()
        {
            tree?.Halt();
        }
    }
}

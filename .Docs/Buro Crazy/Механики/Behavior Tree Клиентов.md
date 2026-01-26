# Behavior Tree для Клиентов

---

## Структура Дерева

```
ClientBehaviorTree (Selector - пробуем стратегии по очереди)
│
├── TryMainGoal (Sequence - главный путь к цели)
│   ├── GoToRegistration
│   ├── WaitInQueue (с таймаутом)
│   ├── GetServed
│   ├── NeedForm? (Decorator: True → GetForm)
│   ├── NeedCash? (Decorator: True → GoToCashier)
│   └── LeaveSuccess
│
├── TryAlternativePath (Sequence - альтернативы при блокировке)
│   ├── QueueTooLong? (Condition)
│   │   └── TryCutInLine → DirectorComplain
│   ├── NoService? (Condition)
│   │   └── DirectorComplain
│   └── LeaveUpset
│
├── GrumblingBehavior (Sequence - управление недовольством)
│   ├── PatienceBelowThreshold? (Condition)
│   │   └── Grumble (с повторением)
│   └── PatienceZero? (Condition)
│       └── EscalateToEnraged / LeavingUpset
│
└── LeaveBehavior (Sequence - завершение)
    └── ExecuteLeave (по любой причине)
```

## Типы Узлов

### Composite (Составные)
| Узел | Описание | Возвращает |
|------|----------|------------|
| `Sequence` | Выполняет детей по порядку | Success если все успех, иначе Failure/Run |
| `Selector` | Пробует детей пока один не успех | Success если один успех, иначе Failure |
| `Parallel` | Выполняет всех детей параллельно | Success/N/F по условию |

### Decorator (Декораторы)
| Узел | Описание |
|------|----------|
| `Invert` | Инвертирует результат |
| `Repeat` | Повторяет N раз или бесконечно |
| `Timeout` | Прерывает детей если > N секунд |
| `Cooldown` | Задержка между выполнениями |
| `Condition` | Проверяет условие (True/False) |

### Leaf (Листья - действия)
| Узел | Описание | Параметры |
|------|----------|-----------|
| `MoveToGoal` | Идёт к точке | target: Waypoint |
| `WaitInQueue` | Ждёт в очереди | maxTime: float |
| `GetServed` | Получает услугу | timeout: float |
| `GoToCashier` | Идёт к кассе | - |
| `GetFormFromTable` | Берёт бланк | - |
| `TryCutInLine` | Пытается пролезть | - |
| `ComplainToDirector` | Жалуется директору | - |
| `Grumble` | Ворчит | message: string |
| `Enrage` | Выходит из себя | duration: float |
| `LeaveSuccess` | Уходит довольным | - |
| `LeaveUpset` | Уходит расстроенным | - |
| `CheckPatience` | Проверяет терпение | returns: HeatLevel |

## HeatLevel (уровни терпения)

```csharp
public enum HeatLevel
{
    Calm,        // 0-30% терпения
    Grumbling,   // 30-70% терпения
    Frustrated,  // 70-90% терпения
    Enraged      // 90-100% терпения
}
```

## Примеры Поведения по Архетипу

### Суетун (suetunFactor > 0.5)
- **PatienceThreshold** ниже (быстрее злится)
- **GrumbleFrequency** ниже (мало предупреждений)
- **Escalation** быстрее в Enraged

### Бабушка (babushkaFactor > 0.5)
- **PatienceThreshold** выше (дольше терпит)
- **GrumbleFrequency** выше (много ворчания)
- **Escalation** медленнее (терпеливая)

## Состояния Клиента в BT

| BT Узел | ClientState | Описание |
|---------|-------------|----------|
| MoveToGoal | MovingToGoal | Движение к цели |
| WaitInQueue | AtWaitingArea / SittingInWaitingArea | Ожидание |
| GetServed | AtRegistration / AtDesk1 / AtDesk2 | Обслуживание |
| Grumble | Grumbling | Ворчание |
| Enrage | Enraged | Ярость |
| LeaveSuccess | Leaving | Уход (успех) |
| LeaveUpset | LeavingUpset | Уход (расстройство) |

## Интеграция с ClientStateMachine

```csharp
public class ClientStateMachine : MonoBehaviour
{
    private ClientBehaviorTree behaviorTree;

    void Start()
    {
        behaviorTree = new ClientBehaviorTree(this);
    }

    void Update()
    {
        behaviorTree.Tick();
    }
}
```

## Приоритеты Стратегий

1. **Главная цель** — попытка выполнить заявленную цель
2. **Альтернативный путь** — если главный заблокирован
3. **Управление недовольством** — ворчание, эскалация
4. **Завершение** — уход (любой исход)

---

## Пример: Клиент со справкой

```
1. Spawn → создаём ClientPathfinding + BT
2. BT Tick → Selector.Evaluate()
3. TryMainGoal.Sequence.Evaluate()
   - GoToRegistration → Success
   - WaitInQueue → Running... (ждёт вызова)
   - GetServed → Success (получил печать)
   - NeedForm? → False (не нужен)
   - NeedCash? → True → GoToCashier → Success
   - LeaveSuccess → Success
4. Клиент ушёл, BT очищается
```

## Пример: Клиент в ярости

```
1. Spawn → BT создаётся
2. WaitInQueue → накапливается Stress
3. Stress > grumblingThreshold → GrumblingBehavior
   - Grumble (показывает "Это несносно!")
   - Stress → 100%
4. PatienceZero? → True
   - EscalateToEnraged (40%) или LeaveUpset (60%)
5. Клиент скандалит или уходит злым
```

---

## TODO: Реализация

- [ ] Базовые классы BT (Node, BT)
- [ ] Composite узлы
- [ ] Decorator узлы
- [ ] Leaf узлы для клиента
- [ ] ClientBehaviorTree (конкретное дерево)
- [ ] Интеграция с ClientStateMachine
- [ ] Тесты и баланс

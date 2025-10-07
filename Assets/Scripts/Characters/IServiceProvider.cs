using UnityEngine;

// Этот "контракт" говорит, что любой, кто его подпишет,
// обязан уметь обслуживать клиентов.
public interface IServiceProvider
{
    // Свойство, которое говорит, свободен ли сотрудник для обслуживания
    bool IsAvailableToServe { get; }

    // Метод, который вернет Transform, к которому должен подойти клиент
    Transform GetClientStandPoint();

    // Метод, который вернет ServicePoint, на котором работает сотрудник
    ServicePoint GetWorkstation();

    // Метод для "передачи" клиента на обслуживание
    void AssignClient(ClientPathfinding client);
}
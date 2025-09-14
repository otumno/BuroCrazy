// Файл: ActionType.cs - РАСШИРЕННАЯ ВЕРСИЯ

public enum ActionType
{
    // Действия Клерка
    ProcessDocument,    // Обработка документа у стойки
    ArchiveDocument,    // Архивирование документа (для Архивариуса)

    // Действия Уборщика
    CleanTrash,         // Уборка мусора
    CleanPuddle,        // Уборка лужи
    CleanDirt,          // Уборка грязи

    // Действия Стажера
    HelpConfusedClient, // Помощь потерявшемуся клиенту
    ServeFromQueue,     // Обслуживание клиента из общей очереди
    DeliverDocuments,   // Доставка пачки документов в архив
    CoverDesk,          // Подмена сотрудника на рабочем месте

    // Действия Охранника
    PatrolWaypoint,     // Достижение одной точки патрулирования
    CatchThief,         // Успешная поимка воришки
    CalmDownViolator    // Успешное усмирение нарушителя
}
namespace Enums
{
    public enum ClientSceneType
    {
        None = 0,
        ClientConflict,      // Конфликт в очереди
        ClientChat,          // Случайный разговор
        LostClient,          // Потерявшийся клиент
        WeatherComment,      // Комментарий о погоде
        DirectorAppears,     // Появление директора
        HappyExit,           // Счастливый уход (необычно довольный)
        GroupGrumble,        // Коллективное ворчание
        SpecialEvent         // Специальное событие
    }
}

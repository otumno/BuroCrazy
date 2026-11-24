/// <summary>
/// Приведение целей клиента к тем, которые умеет обслуживать офис.
/// </summary>
public static class ClientGoalExtensions
{
    /// <summary>
    /// Цели, которые клиент получает, если у архетипа не задан список целей.
    /// </summary>
    // Не случайная цель из всего перечисления: туда входят визиты к директору и подтипы,
    // для которых у клиента без сюжета нет ни документа, ни маршрута.
    public static readonly ClientGoal[] FallbackGoals =
    {
        ClientGoal.GetCertificate1,
        ClientGoal.GetCertificate2,
        ClientGoal.PayTax,
        ClientGoal.AskAndLeave
    };

    /// <summary>
    /// Сводит подтипы из таблицы архетипов (Certificate1A, Form2C…) к базовой цели того же окна.
    /// </summary>
    // Почему сводим, а не учим поток подтипам: регистратура, стажёры, директор и кассир маршрутизируют
    // только базовые цели, а своей механики у подтипов пока нет (ни отдельных бланков, ни отдельных окон).
    // Без этого клиент с подтипом не знал, куда идти, застревал и ничего не платил.
    public static ClientGoal ToServiceGoal(this ClientGoal goal) =>
        goal switch
        {
            ClientGoal.Certificate1A or ClientGoal.Certificate1B or ClientGoal.Certificate1C
                or ClientGoal.Form1A or ClientGoal.Form1B or ClientGoal.Form1C => ClientGoal.GetCertificate1,
            ClientGoal.Certificate2A or ClientGoal.Certificate2B or ClientGoal.Certificate2C
                or ClientGoal.Form2A or ClientGoal.Form2B or ClientGoal.Form2C => ClientGoal.GetCertificate2,
            _ => goal
        };
}

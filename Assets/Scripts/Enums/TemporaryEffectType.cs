public enum TemporaryEffectType
{
    None,
    // Скидки на налоги
    DiscountTax,
    // Скидки на формы 1
    DiscountForm1A,
    DiscountForm1B,
    DiscountForm1C,
    // Скидки на формы 2
    DiscountForm2A,
    DiscountForm2B,
    DiscountForm2C,
    // Скидки на справки
    DiscountCertificate1A,
    DiscountCertificate1B,
    DiscountCertificate1C,
    DiscountCertificate2A,
    DiscountCertificate2B,
    DiscountCertificate2C,
    // Специальные
    CleaningCrew,      // вызов уборщиков
    ClownClients,      // клоун для клиентов
    ClownStaff,        // клоун для персонала
    LoanActive         // активный займ (для отслеживания)
}
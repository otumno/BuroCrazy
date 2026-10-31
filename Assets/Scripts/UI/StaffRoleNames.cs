namespace UI
{
    /// <summary>
    /// Названия ролей сотрудников для интерфейса.
    /// </summary>
    // Такие же switch есть в TeamMemberCardUI, HiringSystemUI и ResumePin. Их не трогаем без запроса,
    // новый UI берёт названия отсюда.
    public static class StaffRoleNames
    {
        public static string GetDisplayName(StaffController.Role role) =>
            role switch
            {
                StaffController.Role.Intern => "Стажёр",
                StaffController.Role.Registrar => "Регистратор",
                StaffController.Role.Cashier => "Кассир",
                StaffController.Role.Archivist => "Архивариус",
                StaffController.Role.Guard => "Охранник",
                StaffController.Role.Janitor => "Уборщик",
                StaffController.Role.Clerk => "Клерк",
                StaffController.Role.OfficeManager => "Офис-менеджер",
                StaffController.Role.Accountant => "Бухгалтер",
                StaffController.Role.ServiceWorker => "Сервисный работник",
                StaffController.Role.Director => "Директор",
                _ => "Не назначено"
            };
    }
}

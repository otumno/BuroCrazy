namespace UI
{
    public static class StaffRoleNames
    {
        public static string GetDisplayName(StaffController.Role role) => GameText.Get(GetKey(role));

        public static string GetKey(StaffController.Role role) => role switch
        {
            StaffController.Role.Intern => "role.intern",
            StaffController.Role.Clerk => "role.clerk",
            StaffController.Role.Registrar => "role.registrar",
            StaffController.Role.Cashier => "role.cashier",
            StaffController.Role.Archivist => "role.archivist",
            StaffController.Role.Guard => "role.guard",
            StaffController.Role.Janitor => "role.janitor",
            StaffController.Role.OfficeManager => "role.office_manager",
            StaffController.Role.Accountant => "role.accountant",
            StaffController.Role.ServiceWorker => "role.service_worker",
            StaffController.Role.Director => "role.director",
            _ => "role.unassigned"
        };
    }
}

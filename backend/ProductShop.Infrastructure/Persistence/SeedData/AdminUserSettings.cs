namespace ProductShop.Infrastructure.Persistence.Seed
{
    // Bound from the "AdminUser" section of appsettings.
    // On startup, DatabaseInitializer creates this admin account if it does not exist yet.
    public class AdminUserSettings
    {
        public const string SectionName = "AdminUser";

        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirstName { get; set; } = "Admin";
        public string LastName { get; set; } = "User";
    }
}

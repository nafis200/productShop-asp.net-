namespace ProductShop.Domain.Entities
{
    // One row in the "Users" table = one person who can log in.
    public class User : BaseEntity
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        // Always stored lower-case (see StringExtensions.NormalizeEmail).
        public string Email { get; set; } = string.Empty;

        // We NEVER store the real password, only a one-way hash of it.
        public string PasswordHash { get; set; } = string.Empty;

        // Navigation property: the roles this user has (Admin, User, ...).
        public List<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}

namespace ProductShop.Domain.Entities
{
    // Join table. One row = "this user has this role". Primary key is (UserId, RoleId).
    public class UserRole
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}

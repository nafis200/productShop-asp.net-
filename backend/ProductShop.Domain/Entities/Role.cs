namespace ProductShop.Domain.Entities
{
    // Lookup table with fixed int ids so the rows can be seeded (see SeedData.cs).
    public class Role
    {
        public int Id { get; set; }

        public required string Name { get; set; }

        public List<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}

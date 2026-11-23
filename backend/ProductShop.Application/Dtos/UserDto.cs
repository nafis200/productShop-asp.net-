namespace ProductShop.Application.Dtos
{
    // What the API sends back about a user. Note: NO PasswordHash here.
    public class UserDto
    {
        public required Guid Id { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Email { get; set; }
        public required List<string> Roles { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

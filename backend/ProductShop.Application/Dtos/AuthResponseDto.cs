namespace ProductShop.Application.Dtos
{
    // Returned by register and login. The frontend stores AccessToken and sends it
    // back in the header "Authorization: Bearer <AccessToken>" on every request.
    public class AuthResponseDto
    {
        public string TokenType => "Bearer";
        public required string AccessToken { get; set; }
        public required DateTime ExpiresAt { get; set; }
        public required UserDto User { get; set; }
    }
}

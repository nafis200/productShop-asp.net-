using System.ComponentModel.DataAnnotations;

namespace ProductShop.Application.Dtos
{
    // JSON body of POST /api/auth/login
    public class LoginDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        public required string Password { get; set; }
    }
}

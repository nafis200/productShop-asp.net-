using System.ComponentModel.DataAnnotations;

namespace ProductShop.Application.Dtos
{
    // JSON body of POST /api/auth/register
    public class RegisterDto
    {
        [Required]
        [StringLength(50)]
        public required string FirstName { get; set; }

        [Required]
        [StringLength(50)]
        public required string LastName { get; set; }

        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 8)]
        public required string Password { get; set; }
    }
}

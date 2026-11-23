using ProductShop.Application.Dtos;
using ProductShop.Domain.Entities;

namespace ProductShop.Application.Mappings
{
    // Entity (database shape)  ->  DTO (JSON shape).
    // Keeps the controllers from ever returning an entity (e.g. User with PasswordHash).
    public static class MappingExtensions
    {
        // Requires user.UserRoles[i].Role to be loaded (the repositories do this with Include).
        public static UserDto ToDto(this User user)
        {
            return new UserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList(),
                CreatedAt = user.CreatedAt
            };
        }

        public static ProductDto ToDto(this Product product)
        {
            return new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price,
                CreatedAt = product.CreatedAt,
                ModifiedAt = product.ModifiedAt
            };
        }
    }
}

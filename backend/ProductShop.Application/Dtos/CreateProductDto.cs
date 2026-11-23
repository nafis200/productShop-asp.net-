using System.ComponentModel.DataAnnotations;
using ProductShop.Domain.Entities;

namespace ProductShop.Application.Dtos
{
    // JSON body of POST /api/products (admin only)
    public class CreateProductDto
    {
        [Required]
        [StringLength(Product.MaxNameLength)]
        public required string Name { get; set; }

        [Range(0.01, 1_000_000)]
        public required decimal Price { get; set; }
    }
}

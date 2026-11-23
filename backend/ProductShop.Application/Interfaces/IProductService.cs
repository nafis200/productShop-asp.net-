using ProductShop.Application.Dtos;

namespace ProductShop.Application.Interfaces
{
    public interface IProductService
    {
        Task<List<ProductDto>> GetAllAsync();
        Task<ProductDto?> GetByIdAsync(Guid id);
        Task<ProductDto> CreateAsync(CreateProductDto createProductDto);

        // Returns null when the product does not exist.
        Task<ProductDto?> UpdateAsync(Guid id, UpdateProductDto updateProductDto);

        // Returns false when the product does not exist.
        Task<bool> DeleteAsync(Guid id);
    }
}

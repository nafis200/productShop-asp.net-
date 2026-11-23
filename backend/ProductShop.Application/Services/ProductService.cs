using ProductShop.Application.Dtos;
using ProductShop.Application.Interfaces;
using ProductShop.Application.Mappings;
using ProductShop.Domain.Entities;

namespace ProductShop.Application.Services
{
    // Business rules for products. Who is ALLOWED to call these (admin or not)
    // is decided in the controller with [Authorize(Roles = ...)].
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;

        public ProductService(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<List<ProductDto>> GetAllAsync()
        {
            var products = await _productRepository.GetAllAsync();

            return products.Select(p => p.ToDto()).ToList();
        }

        public async Task<ProductDto?> GetByIdAsync(Guid id)
        {
            var product = await _productRepository.GetByIdAsync(id);

            return product?.ToDto();
        }

        public async Task<ProductDto> CreateAsync(CreateProductDto createProductDto)
        {
            var product = new Product
            {
                Name = createProductDto.Name.Trim(),
                Price = Math.Round(createProductDto.Price, 2)
            };

            await _productRepository.AddAsync(product);

            return product.ToDto();
        }

        public async Task<ProductDto?> UpdateAsync(Guid id, UpdateProductDto updateProductDto)
        {
            var product = await _productRepository.GetByIdAsync(id);

            if (product is null)
            {
                return null;
            }

            product.Name = updateProductDto.Name.Trim();
            product.Price = Math.Round(updateProductDto.Price, 2);
            product.MarkAsModified();

            await _productRepository.UpdateAsync(product);

            return product.ToDto();
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var product = await _productRepository.GetByIdAsync(id);

            if (product is null)
            {
                return false;
            }

            await _productRepository.DeleteAsync(product);

            return true;
        }
    }
}

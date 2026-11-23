using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductShop.Application.Dtos;
using ProductShop.Application.Interfaces;
using ProductShop.Domain.Constants;

namespace ProductShop.Api.Controllers
{
    //  /api/products
    //  - Any logged-in user (Admin or User) can READ.
    //  - Only Admin can CREATE, UPDATE, DELETE.
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        // GET /api/products
        [HttpGet]
        [ProducesResponseType(typeof(List<ProductDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _productService.GetAllAsync());
        }

        // GET /api/products/{id}
        // ":guid" = this route only matches if {id} is a valid Guid.
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById([FromRoute] Guid id)
        {
            var product = await _productService.GetByIdAsync(id);

            if (product is null)
            {
                return NotFound();
            }

            return Ok(product);
        }

        // POST /api/products
        [HttpPost]
        [Authorize(Roles = RoleNames.Admin)]   // logged in but not Admin -> 403 Forbidden
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] CreateProductDto createProductDto)
        {
            var product = await _productService.CreateAsync(createProductDto);

            // 201 + "Location: /api/products/{id}" header + the product in the body.
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
        }

        // PUT /api/products/{id}
        [HttpPut("{id:guid}")]
        [Authorize(Roles = RoleNames.Admin)]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateProductDto updateProductDto)
        {
            var product = await _productService.UpdateAsync(id, updateProductDto);

            if (product is null)
            {
                return NotFound();
            }

            return Ok(product);
        }

        // DELETE /api/products/{id}
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = RoleNames.Admin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete([FromRoute] Guid id)
        {
            var isDeleted = await _productService.DeleteAsync(id);

            if (!isDeleted)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}

namespace ProductShop.Application.Dtos
{
    // What the API sends back for one product.
    public class ProductDto
    {
        public required Guid Id { get; set; }
        public required string Name { get; set; }
        public required decimal Price { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
    }
}

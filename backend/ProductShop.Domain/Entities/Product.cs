namespace ProductShop.Domain.Entities
{
    // One row in the "Products" table.
    public class Product : BaseEntity
    {
        public const int MaxNameLength = 100;

        public string Name { get; set; } = string.Empty;

        // decimal (not double) because money must be exact.
        public decimal Price { get; set; }
    }
}

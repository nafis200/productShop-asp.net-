namespace ProductShop.Domain.Entities
{
    // Every table that uses a Guid id inherits from this class,
    // so Id / CreatedAt / ModifiedAt are written only once.
    public abstract class BaseEntity
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime? ModifiedAt { get; private set; }

        public void MarkAsModified()
        {
            ModifiedAt = DateTime.UtcNow;
        }
    }
}

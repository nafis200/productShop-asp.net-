using ProductShop.Domain.Constants;
using ProductShop.Domain.Entities;

namespace ProductShop.Infrastructure.Persistence.Seed
{
    // Fixed rows inserted by the migration (HasData). Ids must NEVER change after a migration was applied.
    public static class SeedData
    {
        public static class RoleIds
        {
            public const int Admin = 1;
            public const int User = 2;
        }

        public static readonly Role[] Roles =
        {
            new Role { Id = RoleIds.Admin, Name = RoleNames.Admin },
            new Role { Id = RoleIds.User, Name = RoleNames.User }
        };
    }
}

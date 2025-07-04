using Microsoft.EntityFrameworkCore;
using RolesWarden.Models;

namespace RolesWarden.Db
{
    public sealed class WardenDbContext : DbContext
    {
        public WardenDbContext() : base() { /* Empty */}
        public WardenDbContext(DbContextOptions<WardenDbContext> options) : base(options) { /* Empty */ }

        public DbSet<GuildConfiguration> GuildConfigurations { get; init; } = default!;
        public DbSet<RoleConfiguration> RoleConfigurations { get; init; } = default!;
        public DbSet<SavedRoles> SavedRoles { get; init; } = default!;
    }
}

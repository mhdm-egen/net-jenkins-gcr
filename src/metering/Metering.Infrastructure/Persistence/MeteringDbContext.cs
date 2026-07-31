using Metering.Domain;
using Microsoft.EntityFrameworkCore;

namespace Metering.Infrastructure.Persistence;

public sealed class MeteringDbContext : DbContext
{
    public MeteringDbContext(DbContextOptions<MeteringDbContext> options) : base(options) { }

    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MeteringDbContext).Assembly);
    }
}

using Microsoft.EntityFrameworkCore;
using Publisher.Domain.Channels;
using Publisher.Domain.Containers;
using Publisher.Domain.Promotions;
using Publisher.Domain.Registries;
using Publisher.Domain.Rules;

namespace Publisher.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the publisher model. Concrete
/// <c>IEntityTypeConfiguration&lt;T&gt;</c>s under <c>Persistence/Configurations/</c> are
/// applied via <see cref="ModelBuilder.ApplyConfigurationsFromAssembly"/>.
/// </summary>
public sealed class PublisherDbContext : DbContext
{
    public PublisherDbContext(DbContextOptions<PublisherDbContext> options) : base(options) { }

    public DbSet<PublishableContainer> Containers => Set<PublishableContainer>();
    public DbSet<PublishChannel> Channels => Set<PublishChannel>();
    public DbSet<ChannelBinding> ChannelBindings => Set<ChannelBinding>();
    public DbSet<RemoteRegistry> Registries => Set<RemoteRegistry>();
    public DbSet<AutomationRule> Rules => Set<AutomationRule>();
    public DbSet<Promotion> Promotions => Set<Promotion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PublisherDbContext).Assembly);
    }
}

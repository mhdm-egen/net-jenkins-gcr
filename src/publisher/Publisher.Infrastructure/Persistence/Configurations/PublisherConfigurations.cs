using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Publisher.Domain.Channels;
using Publisher.Domain.Containers;

namespace Publisher.Infrastructure.Persistence.Configurations;

/// <summary>
/// The Nexus-inventory record. Natural key (RepositoryId, ContainerName, Version) is enforced
/// unique so the poll-driven upsert can't create duplicates.
/// </summary>
public sealed class PublishableContainerConfiguration : IEntityTypeConfiguration<PublishableContainer>
{
    public void Configure(EntityTypeBuilder<PublishableContainer> b)
    {
        b.ToTable("PublishableContainer");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.RepositoryId).IsRequired();
        b.Property(c => c.BuildId).IsRequired();
        b.Property(c => c.ContainerName).HasMaxLength(300).IsRequired();
        b.Property(c => c.Version).HasMaxLength(200).IsRequired();
        b.Property(c => c.CommitSha).HasMaxLength(100).IsRequired();
        b.Property(c => c.ArtifactUri).HasMaxLength(1000).IsRequired();
        b.Property(c => c.ImageDigest).HasMaxLength(200);
        b.Property(c => c.FirstSeenAtUtc).IsRequired();
        b.Property(c => c.LastSeenAtUtc).IsRequired();

        b.HasIndex(c => new { c.RepositoryId, c.ContainerName, c.Version }).IsUnique();
    }
}

/// <summary>
/// A publishable name. Name is unique; the current pointer is mirrored on the row and the
/// full move history lives in <see cref="ChannelBinding"/> children (cascade-deleted).
/// </summary>
public sealed class PublishChannelConfiguration : IEntityTypeConfiguration<PublishChannel>
{
    public void Configure(EntityTypeBuilder<PublishChannel> b)
    {
        b.ToTable("PublishChannel");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.Name).HasMaxLength(200).IsRequired();
        b.Property(c => c.CurrentContainerId).IsRequired();
        b.Property(c => c.CreatedAtUtc).IsRequired();
        b.Property(c => c.UpdatedAtUtc).IsRequired();

        b.HasIndex(c => c.Name).IsUnique();

        b.HasMany(c => c.Bindings)
            .WithOne()
            .HasForeignKey(x => x.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(c => c.Bindings).AutoInclude();

        // The backing field is a List<ChannelBinding>; bind EF to it rather than the read-only nav.
        b.Metadata.FindNavigation(nameof(PublishChannel.Bindings))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ChannelBindingConfiguration : IEntityTypeConfiguration<ChannelBinding>
{
    public void Configure(EntityTypeBuilder<ChannelBinding> b)
    {
        b.ToTable("ChannelBinding");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.ChannelId).IsRequired();
        b.Property(x => x.Sequence).IsRequired();
        b.Property(x => x.ContainerId).IsRequired();
        b.Property(x => x.BoundBy).HasMaxLength(200).IsRequired();
        b.Property(x => x.BoundAtUtc).IsRequired();

        b.HasIndex(x => new { x.ChannelId, x.Sequence }).IsUnique();
    }
}

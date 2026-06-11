using Deployment.Domain.ContainerImages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deployment.Infrastructure.Persistence.Configurations.Catalog;

/// <summary>
/// A reusable container-image coordinate (Registry + Repository + Name) backing a
/// deployment Service. The coordinate is unique; <c>BaseRef</c> is a computed view.
/// </summary>
public sealed class ContainerImageConfiguration : IEntityTypeConfiguration<ContainerImage>
{
    public void Configure(EntityTypeBuilder<ContainerImage> b)
    {
        b.ToTable("ContainerImage");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.Registry).HasMaxLength(300).IsRequired();
        b.Property(c => c.Repository).HasMaxLength(300).IsRequired();
        b.Property(c => c.Name).HasMaxLength(300).IsRequired();
        b.Property(c => c.DefaultTag).HasMaxLength(200).IsRequired();
        b.Property(c => c.IsActive).IsRequired();
        b.Property(c => c.CreatedAtUtc).IsRequired();

        b.Ignore(c => c.BaseRef); // computed from Registry/Repository/Name

        b.HasIndex(c => new { c.Registry, c.Repository, c.Name }).IsUnique();
    }
}

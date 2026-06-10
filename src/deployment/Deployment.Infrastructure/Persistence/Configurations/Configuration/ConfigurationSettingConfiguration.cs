using Deployment.Domain.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deployment.Infrastructure.Persistence.Configurations.Configuration;

public sealed class ConfigurationSettingConfiguration : IEntityTypeConfiguration<ConfigurationSetting>
{
    public void Configure(EntityTypeBuilder<ConfigurationSetting> b)
    {
        b.ToTable("ConfigurationSetting", t =>
        {
            // Secret/plain dichotomy at the DB layer mirrors the domain invariant.
            t.HasCheckConstraint("CK_ConfigurationSetting_SecretShape",
                "([IsSecret] = 1 AND [Value] IS NULL AND [SecretReference] IS NOT NULL) " +
                "OR ([IsSecret] = 0 AND [SecretReference] IS NULL AND [Value] IS NOT NULL)");
        });
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.DeployableUnitId).IsRequired();
        b.Property(s => s.EnvironmentId);
        b.Property(s => s.Key).HasMaxLength(500).IsRequired();
        b.Property(s => s.Value).HasMaxLength(4000);
        b.Property(s => s.IsSecret).IsRequired();
        b.Property(s => s.SecretReference).HasMaxLength(500);
        b.Property(s => s.ValueType).HasConversion<int>().IsRequired();

        // Lookup hot-path: resolve config for a unit in an environment.
        b.HasIndex(s => new { s.DeployableUnitId, s.EnvironmentId, s.Key }).IsUnique();
    }
}

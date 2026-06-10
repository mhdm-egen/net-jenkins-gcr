using Deployment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Deployment.Infrastructure.Tests.Persistence;

/// <summary>
/// Validates the EF model builds end-to-end (every mapping resolves, every
/// owned type loads, every navigation matches up) without spinning up a real
/// database. Catches breakage in the mappings layer fast and cheap.
/// </summary>
public class DeploymentDbContextModelTests
{
    [Fact]
    public void Model_builds_with_all_expected_tables()
    {
        using var db = NewContext();
        var tables = db.Model.GetEntityTypes()
            .Where(et => et.GetTableName() is not null)
            .Select(et => et.GetTableName()!)
            .ToHashSet();

        var expected = new[]
        {
            "DeployableUnit", "Service", "Application", "ApplicationService",
            "Release", "ReleaseComposition",
            "Environment", "DeploymentTarget", "EnvironmentFreezeWindow",
            "ConfigurationSetting",
            "Deployment", "Approval", "DeploymentEvent", "DeploymentSecretBinding",
            "ConfigurationSettingHistory", "ReleaseStatusChange",
        };
        foreach (var t in expected) tables.Should().Contain(t);
    }

    [Fact]
    public void Service_and_DeployableUnit_share_primary_key_via_1to1()
    {
        using var db = NewContext();
        var service = db.Model.FindEntityType(typeof(Domain.DeployableUnits.Service))!;
        service.GetTableName().Should().Be("Service");

        var fk = service.GetForeignKeys()
            .Single(f => f.PrincipalEntityType.ClrType == typeof(Domain.DeployableUnits.DeployableUnit));
        fk.Properties.Should().ContainSingle().Which.Name.Should().Be("Id");
        fk.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void Generated_create_script_includes_check_constraints()
    {
        using var db = NewContext();
        var ddl = db.Database.GenerateCreateScript();

        ddl.Should().Contain("CK_ReleaseComposition_PinMode");
        ddl.Should().Contain("CK_ConfigurationSetting_SecretShape");
    }

    [Fact]
    public void Generated_create_script_includes_unique_indexes()
    {
        using var db = NewContext();
        var ddl = db.Database.GenerateCreateScript();

        ddl.Should().Contain("CREATE UNIQUE INDEX");
        // (DeployableUnitId, SemanticVersion) unique on Release.
        ddl.Should().Contain("IX_Release_DeployableUnitId_SemanticVersion");
        // Name unique on DeployableUnit.
        ddl.Should().Contain("IX_DeployableUnit_Name");
        // Name unique on Environment.
        ddl.Should().Contain("IX_Environment_Name");
    }

    private static DeploymentDbContext NewContext()
    {
        var opts = new DbContextOptionsBuilder<DeploymentDbContext>()
            .UseSqlServer("Server=(local);Database=NeverConnects;Trusted_Connection=True;")
            .Options;
        return new DeploymentDbContext(opts);
    }
}

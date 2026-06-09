using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Deployment.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> / <c>dotnet ef database update</c>
/// instantiate a <see cref="DeploymentDbContext"/> without spinning up the
/// host. EF Core only needs the provider and a syntactically valid connection
/// string to produce migration SQL — the placeholder below is never actually
/// connected to. Override via the <c>DEPLOYMENT_CONNECTIONSTRING</c>
/// environment variable if you want to run <c>database update</c> against a
/// real instance from the CLI.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DeploymentDbContext>
{
    public DeploymentDbContext CreateDbContext(string[] args)
    {
        var connection = System.Environment.GetEnvironmentVariable("DEPLOYMENT_CONNECTIONSTRING")
            ?? "Server=localhost;Database=Deployment;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";

        var opts = new DbContextOptionsBuilder<DeploymentDbContext>()
            .UseSqlServer(connection, b => b.MigrationsAssembly(typeof(DeploymentDbContext).Assembly.GetName().Name))
            .Options;

        return new DeploymentDbContext(opts);
    }
}

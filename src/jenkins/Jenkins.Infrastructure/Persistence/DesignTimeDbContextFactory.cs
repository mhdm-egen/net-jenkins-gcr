using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Jenkins.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> / <c>database update</c> instantiate a
/// <see cref="JenkinsCiDbContext"/> without the host. The connection string only needs to
/// be valid for the SQL Server provider to emit migration SQL — no live DB is required to
/// scaffold. Override via the <c>JENKINSCI_CONNECTIONSTRING</c> environment variable.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<JenkinsCiDbContext>
{
    public JenkinsCiDbContext CreateDbContext(string[] args)
    {
        var connection = System.Environment.GetEnvironmentVariable("JENKINSCI_CONNECTIONSTRING")
            ?? "Server=localhost;Database=JenkinsCi;Trusted_Connection=True;TrustServerCertificate=True";

        var opts = new DbContextOptionsBuilder<JenkinsCiDbContext>()
            .UseSqlServer(connection, b => b.MigrationsAssembly(typeof(JenkinsCiDbContext).Assembly.GetName().Name))
            .Options;

        return new JenkinsCiDbContext(opts);
    }
}

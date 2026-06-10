using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Jenkins.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> / <c>database update</c> instantiate a
/// <see cref="JenkinsCiDbContext"/> without the host. SQLite only needs a valid
/// connection string to emit migration SQL. Override the file via the
/// <c>JENKINSCI_CONNECTIONSTRING</c> environment variable to run against a real DB.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<JenkinsCiDbContext>
{
    public JenkinsCiDbContext CreateDbContext(string[] args)
    {
        var connection = System.Environment.GetEnvironmentVariable("JENKINSCI_CONNECTIONSTRING")
            ?? "Data Source=jenkins-ci.db";

        var opts = new DbContextOptionsBuilder<JenkinsCiDbContext>()
            .UseSqlite(connection, b => b.MigrationsAssembly(typeof(JenkinsCiDbContext).Assembly.GetName().Name))
            .Options;

        return new JenkinsCiDbContext(opts);
    }
}

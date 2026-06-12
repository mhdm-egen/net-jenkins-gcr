using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Publisher.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> / <c>dotnet ef database update</c> instantiate a
/// <see cref="PublisherDbContext"/> without spinning up the host. EF Core only needs the
/// provider and a syntactically valid connection string to produce migration SQL — the
/// placeholder below is never actually connected to. Override via the
/// <c>PUBLISHER_CONNECTIONSTRING</c> environment variable to run <c>database update</c>
/// against a real instance from the CLI.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PublisherDbContext>
{
    public PublisherDbContext CreateDbContext(string[] args)
    {
        var connection = System.Environment.GetEnvironmentVariable("PUBLISHER_CONNECTIONSTRING")
            ?? "Server=localhost;Database=Publisher;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";

        var opts = new DbContextOptionsBuilder<PublisherDbContext>()
            .UseSqlServer(connection, b => b.MigrationsAssembly(typeof(PublisherDbContext).Assembly.GetName().Name))
            .Options;

        return new PublisherDbContext(opts);
    }
}

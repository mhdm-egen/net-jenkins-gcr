using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Metering.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef</c> build the context without a running host (design-time only).</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MeteringDbContext>
{
    public MeteringDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("METERING_CONNECTIONSTRING")
            ?? "Server=localhost;Database=Metering;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        var opts = new DbContextOptionsBuilder<MeteringDbContext>()
            .UseSqlServer(connection, b => b.MigrationsAssembly(typeof(MeteringDbContext).Assembly.GetName().Name))
            .Options;
        return new MeteringDbContext(opts);
    }
}

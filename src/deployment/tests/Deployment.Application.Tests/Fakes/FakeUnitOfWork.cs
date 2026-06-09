using Deployment.Domain.Abstractions;

namespace Deployment.Application.Tests.Fakes;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}

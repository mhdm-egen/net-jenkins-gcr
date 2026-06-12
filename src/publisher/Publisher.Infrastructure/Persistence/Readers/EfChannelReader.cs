using Microsoft.EntityFrameworkCore;
using Publisher.Application.Features.Channels;
using Publisher.Contracts.Channels;

namespace Publisher.Infrastructure.Persistence.Readers;

public sealed class EfChannelReader : IChannelReader
{
    private readonly PublisherDbContext _db;

    public EfChannelReader(PublisherDbContext db) => _db = db;

    public async Task<IReadOnlyList<PublishChannelDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var channels = await _db.Channels.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.CurrentContainerId, c.CreatedAtUtc, c.UpdatedAtUtc })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var ids = channels.Select(c => c.Id).ToList();
        var bindings = await _db.ChannelBindings.AsNoTracking()
            .Where(b => ids.Contains(b.ChannelId))
            .OrderBy(b => b.Sequence)
            .Select(b => new { b.ChannelId, b.Sequence, b.ContainerId, b.BoundBy, b.BoundAtUtc })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var byChannel = bindings.ToLookup(b => b.ChannelId);
        return channels.Select(c => new PublishChannelDto(
            c.Id, c.Name, c.CurrentContainerId, c.CreatedAtUtc, c.UpdatedAtUtc,
            byChannel[c.Id]
                .Select(b => new ChannelBindingDto(b.Sequence, b.ContainerId, b.BoundBy, b.BoundAtUtc))
                .ToList())).ToList();
    }

    public async Task<PublishChannelDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var n = name.Trim();
        var c = await _db.Channels.AsNoTracking()
            .Where(x => x.Name == n)
            .Select(x => new { x.Id, x.Name, x.CurrentContainerId, x.CreatedAtUtc, x.UpdatedAtUtc })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (c is null) return null;

        var history = await _db.ChannelBindings.AsNoTracking()
            .Where(b => b.ChannelId == c.Id)
            .OrderBy(b => b.Sequence)
            .Select(b => new ChannelBindingDto(b.Sequence, b.ContainerId, b.BoundBy, b.BoundAtUtc))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PublishChannelDto(c.Id, c.Name, c.CurrentContainerId, c.CreatedAtUtc, c.UpdatedAtUtc, history);
    }

    public async Task<IReadOnlyList<string>> ListChannelNamesForContainerAsync(Guid containerId, CancellationToken cancellationToken = default)
        => await _db.Channels.AsNoTracking()
            .Where(c => c.CurrentContainerId == containerId)
            .OrderBy(c => c.Name)
            .Select(c => c.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
}

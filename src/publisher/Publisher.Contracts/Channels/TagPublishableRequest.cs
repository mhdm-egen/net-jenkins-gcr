namespace Publisher.Contracts.Channels;

/// <summary>
/// Body for <c>PUT /api/publisher/channels/{name}</c> — tag a specific inventory container as
/// publishable under the channel name in the route. Creates the channel if it does not exist,
/// otherwise moves the pointer to <see cref="ContainerId"/>.
/// </summary>
public sealed record TagPublishableRequest(Guid ContainerId, string? BoundBy);

using Publisher.Contracts.Rules;

namespace Publisher.Application.Features.Rules;

public interface IRuleReader
{
    Task<IReadOnlyList<AutomationRuleDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<AutomationRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record ListRulesQuery;

public sealed class ListRulesHandler
{
    private readonly IRuleReader _reader;
    public ListRulesHandler(IRuleReader reader) => _reader = reader;

    public Task<IReadOnlyList<AutomationRuleDto>> HandleAsync(ListRulesQuery query, CancellationToken cancellationToken = default)
        => _reader.ListAsync(cancellationToken);
}

public sealed record GetRuleByIdQuery(Guid Id);

public sealed class GetRuleByIdHandler
{
    private readonly IRuleReader _reader;
    public GetRuleByIdHandler(IRuleReader reader) => _reader = reader;

    public Task<AutomationRuleDto?> HandleAsync(GetRuleByIdQuery query, CancellationToken cancellationToken = default)
        => _reader.GetByIdAsync(query.Id, cancellationToken);
}

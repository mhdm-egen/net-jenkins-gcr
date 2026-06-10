using Jenkins.Domain.Common;
using Jenkins.Domain.Pipelines.Events;

namespace Jenkins.Domain.Pipelines;

/// <summary>
/// A named, ordered sequence of Jenkins jobs the orchestrator can run — a
/// user-managed, persisted pipeline (vs. the old hardcoded one). Stages are kept in
/// an explicit <see cref="PipelineStage.Order"/>; <see cref="Stages"/> returns them
/// sorted. Linear model: a stage's optional upstream is identified by job name
/// (matching the orchestrator's <c>SOURCE_BUILD_NUMBER</c> forwarding).
/// </summary>
public sealed class Pipeline : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private readonly List<PipelineStage> _stages = new();
    public IReadOnlyList<PipelineStage> Stages =>
        _stages.OrderBy(s => s.Order).ToList();

    private Pipeline()
    {
        Name = string.Empty;
    }

    public Pipeline(Guid id, string name, string? description, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Id = id;
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
        RaiseEvent(new PipelineCreated(Id, Name, createdAtUtc));
    }

    public void Rename(string name, string? description, DateTimeOffset occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        RaiseEvent(new PipelineRenamed(Id, Name, occurredAtUtc));
    }

    public void SetActive(bool active, DateTimeOffset occurredAtUtc)
    {
        if (IsActive == active) return;
        IsActive = active;
        RaiseEvent(new PipelineActivationChanged(Id, IsActive, occurredAtUtc));
    }

    // --- Stages ---

    public PipelineStage AddStage(
        Guid stageId,
        string jobName,
        string? upstreamJobName,
        IReadOnlyDictionary<string, string>? parameters,
        DateTimeOffset occurredAtUtc)
    {
        var order = _stages.Count == 0 ? 0 : _stages.Max(s => s.Order) + 1;
        var stage = new PipelineStage(stageId, Id, order, jobName, upstreamJobName, parameters);
        _stages.Add(stage);
        RaiseEvent(new PipelineStagesChanged(Id, _stages.Count, occurredAtUtc));
        return stage;
    }

    public void UpdateStage(
        Guid stageId,
        string jobName,
        string? upstreamJobName,
        IReadOnlyDictionary<string, string>? parameters,
        DateTimeOffset occurredAtUtc)
    {
        var stage = _stages.FirstOrDefault(s => s.Id == stageId)
            ?? throw new InvalidOperationException($"Stage {stageId} not found on pipeline {Id}.");
        stage.Update(jobName, upstreamJobName, parameters);
        RaiseEvent(new PipelineStagesChanged(Id, _stages.Count, occurredAtUtc));
    }

    public void RemoveStage(Guid stageId, DateTimeOffset occurredAtUtc)
    {
        var stage = _stages.FirstOrDefault(s => s.Id == stageId)
            ?? throw new InvalidOperationException($"Stage {stageId} not found on pipeline {Id}.");
        _stages.Remove(stage);
        Recompact();
        RaiseEvent(new PipelineStagesChanged(Id, _stages.Count, occurredAtUtc));
    }

    /// <summary>Reassign stage order to the given id sequence (must list every stage exactly once).</summary>
    public void ReorderStages(IReadOnlyList<Guid> orderedStageIds, DateTimeOffset occurredAtUtc)
    {
        if (orderedStageIds.Count != _stages.Count || orderedStageIds.Distinct().Count() != _stages.Count
            || orderedStageIds.Any(id => _stages.All(s => s.Id != id)))
            throw new InvalidOperationException("Reorder must list every stage of the pipeline exactly once.");

        for (var i = 0; i < orderedStageIds.Count; i++)
            _stages.First(s => s.Id == orderedStageIds[i]).SetOrder(i);

        RaiseEvent(new PipelineStagesChanged(Id, _stages.Count, occurredAtUtc));
    }

    private void Recompact()
    {
        var ordered = _stages.OrderBy(s => s.Order).ToList();
        for (var i = 0; i < ordered.Count; i++) ordered[i].SetOrder(i);
    }
}

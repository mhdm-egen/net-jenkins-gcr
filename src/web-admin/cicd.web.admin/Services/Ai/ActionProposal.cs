namespace Cicd.Web.Admin.Services.Ai;

public enum ProposedActionKind
{
    Promote,
    Rollback,
    Approve,
    Reject,
    TeardownPreview,
}

/// <summary>
/// An action the agent thinks should happen — and which a human must actually carry out.
///
/// **A proposal performs nothing.** It is a record with a link. <see cref="Href"/> points at the
/// existing page where the action's own confirm gate lives, so the write still travels the one
/// audited code path it always did. Nothing here is a shortcut around that gate; it is a shortcut
/// to it.
/// </summary>
public sealed record ActionProposal(
    ProposedActionKind Kind,
    string TargetLabel,
    string Reason,
    string Href)
{
    public string Verb => Kind switch
    {
        ProposedActionKind.Promote         => "Promote",
        ProposedActionKind.Rollback        => "Roll back",
        ProposedActionKind.Approve         => "Approve",
        ProposedActionKind.Reject          => "Reject",
        ProposedActionKind.TeardownPreview => "Tear down",
        _                                  => Kind.ToString(),
    };

    /// <summary>
    /// True for actions that discard or undo work. The UI colours these differently — an agent
    /// suggesting a rollback deserves more scrutiny than one suggesting an approval.
    /// </summary>
    public bool IsDestructive =>
        Kind is ProposedActionKind.Rollback
             or ProposedActionKind.Reject
             or ProposedActionKind.TeardownPreview;
}

/// <summary>
/// The outcome of the agent asking to propose something. A refusal is not an error — it is the
/// mechanism that stops the agent suggesting an action the platform would not allow anyway.
/// </summary>
public sealed record ProposalOutcome(bool Accepted, string Message, ActionProposal? Proposal = null);

namespace Deployment.Infrastructure.Aspirate;

/// <summary>
/// Options for the Aspir8 (<c>aspirate</c>) shell-out. Bound from <c>Deployment:Aspirate</c>. The CLI
/// must be installed (<c>dotnet tool install -g Aspirate</c>) and on PATH, or point
/// <see cref="Executable"/> at its full path. Cluster auth is ambient (the kube context's kubeconfig).
/// </summary>
public sealed class AspireOptions
{
    public const string SectionName = "Deployment:Aspirate";

    /// <summary>The aspirate executable — a bare name resolved on PATH, or a full path.</summary>
    public string Executable { get; set; } = "aspirate";

    /// <summary>Image pull policy written into the generated manifests.</summary>
    public string ImagePullPolicy { get; set; } = "IfNotPresent";

    /// <summary>How long <c>aspirate generate</c> may run (builds the Aspire manifest).</summary>
    public int GenerateTimeoutSeconds { get; set; } = 600;

    /// <summary>How long <c>aspirate apply</c> may run.</summary>
    public int ApplyTimeoutSeconds { get; set; } = 300;
}

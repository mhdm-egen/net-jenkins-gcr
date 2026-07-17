using Util.Commands.Abstractions;
using Util.Commands.Nexus;
using Util.Commands.Pipeline;
using Util.Commands.Seed;

namespace Util.Commands;

public static class CommandRegistry
{
    public static IReadOnlyDictionary<string, ICommand> Commands { get; } = BuildRegistry();

    private static IReadOnlyDictionary<string, ICommand> BuildRegistry()
    {
        var commands = new ICommand[]
        {
            new PurgeRepoCommand(),
            new RunPipelineCommand(),
            new SeedDemoCommand(),
        };
        return commands.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
    }
}

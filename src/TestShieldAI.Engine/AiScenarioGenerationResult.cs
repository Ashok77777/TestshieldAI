namespace TestShieldAI.Engine;

public sealed class AiScenarioGenerationResult
{
    public AiScenarioGenerationResult(
        IReadOnlyList<AiTestScenario> scenarios,
        IReadOnlyList<string> warnings,
        bool succeeded)
    {
        Scenarios = scenarios;
        Warnings = warnings;
        Succeeded = succeeded;
    }

    public IReadOnlyList<AiTestScenario> Scenarios { get; }

    public IReadOnlyList<string> Warnings { get; }

    public bool Succeeded { get; }
}

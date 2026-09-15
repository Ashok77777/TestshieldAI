namespace TestShieldAI.Engine;

public sealed class AiScenarioGeneratorOptions
{
    public const int DefaultMaxScenariosPerOperation = 6;

    public int MaxScenariosPerOperation { get; set; } = DefaultMaxScenariosPerOperation;
}

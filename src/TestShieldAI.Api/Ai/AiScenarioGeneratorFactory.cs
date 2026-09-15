using TestShieldAI.Engine;

namespace TestShieldAI.Api.Ai;

public static class AiScenarioGeneratorFactory
{
    public static IAiScenarioGenerator Create(AiOptions? options, IAiCompletionClient completionClient)
    {
        if (AiCompletionClientFactory.UseNoOp(options))
        {
            return new NoOpAiScenarioGenerator();
        }

        ArgumentNullException.ThrowIfNull(completionClient);
        return new LlmAiScenarioGenerator(
            completionClient,
            new AiScenarioGeneratorOptions
            {
                MaxScenariosPerOperation = options!.MaxScenariosPerOperation > 0
                    ? options.MaxScenariosPerOperation
                    : AiScenarioGeneratorOptions.DefaultMaxScenariosPerOperation
            });
    }
}

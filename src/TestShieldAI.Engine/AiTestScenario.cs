namespace TestShieldAI.Engine;

public sealed class AiTestScenario
{
    public AiTestScenario(
        AiScenarioType type,
        string method,
        string pathTemplate,
        string path,
        IReadOnlyList<GeneratedApiParameter> parameters,
        string? requestBody,
        int expectedStatus,
        ImportedSchema? expectedResponseSchema,
        string? rationale,
        string sourceMethod,
        string sourcePath)
    {
        Type = type;
        Method = method;
        PathTemplate = pathTemplate;
        Path = path;
        Parameters = parameters;
        RequestBody = requestBody;
        ExpectedStatus = expectedStatus;
        ExpectedResponseSchema = expectedResponseSchema;
        Rationale = rationale;
        SourceMethod = sourceMethod;
        SourcePath = sourcePath;
    }

    public AiScenarioType Type { get; }

    public string Method { get; }

    public string PathTemplate { get; }

    public string Path { get; }

    public IReadOnlyList<GeneratedApiParameter> Parameters { get; }

    public string? RequestBody { get; }

    public int ExpectedStatus { get; }

    public ImportedSchema? ExpectedResponseSchema { get; }

    public string? Rationale { get; }

    public string SourceMethod { get; }

    public string SourcePath { get; }
}

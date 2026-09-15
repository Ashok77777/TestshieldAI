namespace TestShieldAI.Engine;

public sealed class GeneratedApiTestCase
{
    public GeneratedApiTestCase(
        GeneratedApiTestKind kind,
        string method,
        string pathTemplate,
        string path,
        IReadOnlyList<GeneratedApiParameter> parameters,
        string? requestBody,
        int expectedStatus,
        ImportedSchema? expectedResponseSchema,
        string sourceMethod,
        string sourcePath,
        string? scenarioKey = null,
        string? rationale = null,
        string? specKey = null)
    {
        Kind = kind;
        Method = method;
        PathTemplate = pathTemplate;
        Path = path;
        Parameters = parameters;
        RequestBody = requestBody;
        ExpectedStatus = expectedStatus;
        ExpectedResponseSchema = expectedResponseSchema;
        SourceMethod = sourceMethod;
        SourcePath = sourcePath;
        ScenarioKey = scenarioKey;
        Rationale = rationale;
        SpecKey = specKey;
    }

    public GeneratedApiTestKind Kind { get; }

    public string Method { get; }

    public string PathTemplate { get; }

    public string Path { get; }

    public IReadOnlyList<GeneratedApiParameter> Parameters { get; }

    public string? RequestBody { get; }

    public int ExpectedStatus { get; }

    public ImportedSchema? ExpectedResponseSchema { get; }

    public string SourceMethod { get; }

    public string SourcePath { get; }

    public string? ScenarioKey { get; }

    public string? Rationale { get; }

    public string? SpecKey { get; }
}

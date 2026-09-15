namespace TestShieldAI.Engine;

public sealed class RegressionFinding
{
    public RegressionFinding(
        RegressionFindingCategory category,
        string code,
        RegressionFindingSeverity severity,
        GeneratedApiTestKind? kind,
        string method,
        string path,
        string expected,
        string current,
        string? jsonPath,
        string message)
    {
        Category = category;
        Code = code;
        Severity = severity;
        Kind = kind;
        Method = method;
        Path = path;
        Expected = expected;
        Current = current;
        JsonPath = jsonPath;
        Message = message;
    }

    public RegressionFindingCategory Category { get; }

    public string Code { get; }

    public RegressionFindingSeverity Severity { get; }

    public GeneratedApiTestKind? Kind { get; }

    public string Method { get; }

    public string Path { get; }

    public string Expected { get; }

    public string Current { get; }

    public string? JsonPath { get; }

    public string Message { get; }
}

namespace TestShieldAI.Engine;

public sealed class ApiTestExecutionResult
{
    public ApiTestExecutionResult(
        GeneratedApiTestKind kind,
        string method,
        string path,
        string sourceMethod,
        string sourcePath,
        int expectedStatus,
        int? actualStatus,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        int durationMs,
        bool statusMatched,
        string? error)
    {
        Kind = kind;
        Method = method;
        Path = path;
        SourceMethod = sourceMethod;
        SourcePath = sourcePath;
        ExpectedStatus = expectedStatus;
        ActualStatus = actualStatus;
        Headers = headers;
        Body = body;
        DurationMs = durationMs;
        StatusMatched = statusMatched;
        Error = error;
    }

    public GeneratedApiTestKind Kind { get; }

    public string Method { get; }

    public string Path { get; }

    public string SourceMethod { get; }

    public string SourcePath { get; }

    public int ExpectedStatus { get; }

    public int? ActualStatus { get; }

    public IReadOnlyDictionary<string, string> Headers { get; }

    public string? Body { get; }

    public int DurationMs { get; }

    public bool StatusMatched { get; }

    public string? Error { get; }
}

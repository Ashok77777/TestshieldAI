namespace TestShieldAI.Engine;

public sealed class ImportedOperation
{
    public ImportedOperation(
        string method,
        string path,
        IReadOnlyList<ImportedParameter> parameters,
        ImportedSchema? requestBody,
        IReadOnlyDictionary<string, ImportedSchema?> responses)
    {
        Method = method;
        Path = path;
        Parameters = parameters;
        RequestBody = requestBody;
        Responses = responses;
    }

    public string Method { get; }

    public string Path { get; }

    public IReadOnlyList<ImportedParameter> Parameters { get; }

    public ImportedSchema? RequestBody { get; }

    public IReadOnlyDictionary<string, ImportedSchema?> Responses { get; }
}

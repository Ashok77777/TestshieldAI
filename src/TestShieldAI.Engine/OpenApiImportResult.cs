namespace TestShieldAI.Engine;

public sealed class OpenApiImportResult
{
    public OpenApiImportResult(
        IReadOnlyList<ImportedOperation> operations,
        string? title = null,
        string? version = null)
    {
        Operations = operations ?? [];
        Title = title;
        Version = version;
        SpecKey = OpenApiSpecKey.FromTitle(title);
    }

    public IReadOnlyList<ImportedOperation> Operations { get; }

    public string? Title { get; }

    public string? Version { get; }

    public string SpecKey { get; }
}

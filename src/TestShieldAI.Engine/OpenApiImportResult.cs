namespace TestShieldAI.Engine;

public sealed class OpenApiImportResult
{
    public OpenApiImportResult(IReadOnlyList<ImportedOperation> operations)
    {
        Operations = operations;
    }

    public IReadOnlyList<ImportedOperation> Operations { get; }
}

namespace TestShieldAI.Engine;

public interface IApiTestGenerator
{
    IReadOnlyList<GeneratedApiTestCase> Generate(IReadOnlyList<ImportedOperation> operations);
}

namespace TestShieldAI.Engine;

public interface IOpenApiIngestor
{
    OpenApiImportResult Import(string spec);
}

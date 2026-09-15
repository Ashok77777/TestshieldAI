using TestShieldAI.Api.Persistence;
using TestShieldAI.Engine;

namespace TestShieldAI.Api.OpenApi;

public sealed class SelectedOpenApiSpecification
{
    public SelectedOpenApiSpecification(
        string specKey,
        OpenApiImportResult import,
        DateTimeOffset importedAt)
    {
        SpecKey = specKey;
        Import = import;
        ImportedAt = importedAt;
    }

    public string SpecKey { get; }

    public OpenApiImportResult Import { get; }

    public DateTimeOffset ImportedAt { get; }
}

public static class CurrentOpenApiSpecificationSelector
{
    public static IReadOnlyList<SelectedOpenApiSpecification> SelectLatestPerSpecKey(
        IReadOnlyList<OpenApiSpecificationRecord> records,
        IOpenApiIngestor ingestor)
    {
        records ??= [];
        var selected = new Dictionary<string, SelectedOpenApiSpecification>(StringComparer.Ordinal);

        foreach (var record in records
                     .OrderByDescending(item => item.ImportedAt)
                     .ThenByDescending(item => item.Id))
        {
            OpenApiImportResult imported;
            try
            {
                imported = ingestor.Import(record.RawSpecification);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            var specKey = OpenApiSpecKey.FromTitle(imported.Title);
            var comparison = OpenApiSpecKey.ComparisonKey(specKey);
            if (selected.ContainsKey(comparison))
            {
                continue;
            }

            selected[comparison] = new SelectedOpenApiSpecification(specKey, imported, record.ImportedAt);
        }

        return selected.Values
            .OrderBy(item => item.SpecKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

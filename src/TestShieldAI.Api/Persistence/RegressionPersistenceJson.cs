using System.Text.Json;
using System.Text.Json.Serialization;
using TestShieldAI.Api.Contracts;
using TestShieldAI.Engine;

namespace TestShieldAI.Api.Persistence;

public static class RegressionPersistenceJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string SerializeSnapshots(IReadOnlyList<RegressionTestSnapshot> snapshots) =>
        JsonSerializer.Serialize(snapshots.Select(ToDto).ToList(), Options);

    public static IReadOnlyList<RegressionTestSnapshot> DeserializeSnapshots(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var dtos = JsonSerializer.Deserialize<List<SnapshotDto>>(json, Options) ?? [];
        return dtos.Select(ToSnapshot).ToList();
    }

    public static string SerializeResults(PersistedTestRunResults results) =>
        JsonSerializer.Serialize(results, Options);

    public static PersistedTestRunResults DeserializeResults(string json) =>
        JsonSerializer.Deserialize<PersistedTestRunResults>(json, Options)
        ?? new PersistedTestRunResults { Tests = [], Findings = [] };

    private static SnapshotDto ToDto(RegressionTestSnapshot snapshot) =>
        new()
        {
            Kind = snapshot.Kind,
            SourceMethod = snapshot.SourceMethod,
            SourcePath = snapshot.SourcePath,
            Method = snapshot.Method,
            PathTemplate = snapshot.PathTemplate,
            ExpectedStatus = snapshot.ExpectedStatus,
            ExpectedResponseSchema = ImportSpecMapper.ToDto(snapshot.ExpectedResponseSchema),
            Outcome = snapshot.Outcome,
            ScenarioKey = snapshot.ScenarioKey,
            SpecKey = snapshot.SpecKey
        };

    private static RegressionTestSnapshot ToSnapshot(SnapshotDto dto) =>
        new(
            dto.Kind,
            dto.SourceMethod,
            dto.SourcePath,
            dto.Method,
            dto.PathTemplate,
            dto.ExpectedStatus,
            ToSchema(dto.ExpectedResponseSchema),
            dto.Outcome,
            dto.ScenarioKey,
            dto.SpecKey);

    private static ImportedSchema? ToSchema(ImportedSchemaDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        return new ImportedSchema(
            dto.Type,
            dto.Required,
            dto.Properties.ToDictionary(
                pair => pair.Key,
                pair => ToSchema(pair.Value)!,
                StringComparer.Ordinal),
            dto.Enum,
            ToSchema(dto.Items));
    }

    private sealed class SnapshotDto
    {
        public GeneratedApiTestKind Kind { get; set; }

        public string SourceMethod { get; set; } = "";

        public string SourcePath { get; set; } = "";

        public string Method { get; set; } = "";

        public string PathTemplate { get; set; } = "";

        public int ExpectedStatus { get; set; }

        public ImportedSchemaDto? ExpectedResponseSchema { get; set; }

        public ContractValidationOutcome Outcome { get; set; }

        public string? ScenarioKey { get; set; }

        public string? SpecKey { get; set; }
    }
}

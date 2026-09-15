using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TestShieldAI.Engine;

public sealed class AiScenarioNormalizer : IAiScenarioNormalizer
{
    public const int MaxScenariosPerOperation = 6;
    public const int MaxScenariosPerType = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public AiScenarioNormalizationResult Normalize(
        IReadOnlyList<AiTestScenario> proposals,
        IReadOnlyList<ImportedOperation> operations,
        IReadOnlyList<GeneratedApiTestCase> existingTests)
    {
        proposals ??= [];
        operations ??= [];
        existingTests ??= [];

        var accepted = new List<GeneratedApiTestCase>();
        var warnings = new List<string>();
        var acceptedKeys = new HashSet<string>(StringComparer.Ordinal);
        var acceptedExecutable = existingTests
            .Select(ExecutableFingerprint)
            .ToHashSet(StringComparer.Ordinal);
        var counts = new Dictionary<OperationIdentity, TypeCounts>();

        foreach (var proposal in proposals)
        {
            if (!TryNormalize(proposal, operations, acceptedKeys, acceptedExecutable, counts, warnings, out var test))
            {
                continue;
            }

            accepted.Add(test);
            acceptedKeys.Add(test.ScenarioKey!);
            acceptedExecutable.Add(ExecutableFingerprint(test));
            Increment(counts, Operation(test), test.Kind);
        }

        return new AiScenarioNormalizationResult(accepted, warnings);
    }

    private static bool TryNormalize(
        AiTestScenario proposal,
        IReadOnlyList<ImportedOperation> operations,
        HashSet<string> acceptedKeys,
        HashSet<string> acceptedExecutable,
        Dictionary<OperationIdentity, TypeCounts> counts,
        List<string> warnings,
        out GeneratedApiTestCase test)
    {
        test = null!;
        if (proposal is null ||
            string.IsNullOrWhiteSpace(proposal.Method) ||
            string.IsNullOrWhiteSpace(proposal.PathTemplate))
        {
            warnings.Add("Incomplete scenario: method and path are required.");
            return false;
        }

        if (!TryMatchOperation(proposal, operations, warnings, out var operation))
        {
            return false;
        }

        if (!TryAcceptParameters(proposal, operation, warnings, out var parameters))
        {
            return false;
        }

        if (!TryAcceptBody(proposal, operation, warnings, out var body))
        {
            return false;
        }

        var kind = MapKind(proposal.Type);
        var (status, schema) = ReconcileResponse(proposal, operation);
        var scenarioKey = ScenarioKey(operation.Method, operation.Path, kind, body, parameters, status);
        if (!acceptedKeys.Add(scenarioKey))
        {
            warnings.Add($"Duplicate AI scenario for {operation.Method} {operation.Path}.");
            return false;
        }

        var extra = new GeneratedApiTestCase(
            kind,
            operation.Method,
            operation.Path,
            SubstitutePath(operation.Path, parameters),
            parameters,
            body,
            status,
            schema,
            operation.Method,
            operation.Path,
            scenarioKey,
            proposal.Rationale,
            operation.SpecKey);

        var executable = ExecutableFingerprint(extra);
        if (!acceptedExecutable.Add(executable))
        {
            warnings.Add($"Existing test already covers {operation.Method} {operation.Path}.");
            acceptedKeys.Remove(scenarioKey);
            return false;
        }

        var identity = Operation(extra);
        var current = counts.GetValueOrDefault(identity);
        if (current.Total >= MaxScenariosPerOperation)
        {
            warnings.Add($"Maximum AI scenarios reached for {operation.Method} {operation.Path}.");
            acceptedKeys.Remove(scenarioKey);
            acceptedExecutable.Remove(executable);
            return false;
        }

        if (CountFor(current, kind) >= MaxScenariosPerType)
        {
            warnings.Add($"Maximum {kind} scenarios reached for {operation.Method} {operation.Path}.");
            acceptedKeys.Remove(scenarioKey);
            acceptedExecutable.Remove(executable);
            return false;
        }

        test = extra;
        return true;
    }

    private static bool TryMatchOperation(
        AiTestScenario proposal,
        IReadOnlyList<ImportedOperation> operations,
        List<string> warnings,
        out ImportedOperation operation)
    {
        operation = null!;
        var samePath = operations
            .Where(candidate => string.Equals(candidate.Path, proposal.PathTemplate, StringComparison.Ordinal))
            .ToList();
        if (samePath.Count == 0)
        {
            warnings.Add($"Unknown endpoint '{proposal.Method} {proposal.PathTemplate}'.");
            return false;
        }

        var match = samePath.FirstOrDefault(candidate =>
            string.Equals(candidate.Method, proposal.Method, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            warnings.Add($"Unsupported HTTP method '{proposal.Method}' for '{proposal.PathTemplate}'.");
            return false;
        }

        operation = match;
        return true;
    }

    private static bool TryAcceptParameters(
        AiTestScenario proposal,
        ImportedOperation operation,
        List<string> warnings,
        out IReadOnlyList<GeneratedApiParameter> parameters)
    {
        var accepted = new List<GeneratedApiParameter>();
        foreach (var proposed in proposal.Parameters ?? [])
        {
            if (proposed is null ||
                string.IsNullOrWhiteSpace(proposed.Name) ||
                string.IsNullOrWhiteSpace(proposed.Location))
            {
                warnings.Add($"Incomplete parameter on {operation.Method} {operation.Path}.");
                parameters = [];
                return false;
            }

            var location = proposed.Location.Trim().ToLowerInvariant();
            if (location is not ("path" or "query" or "header"))
            {
                warnings.Add($"Unknown parameter '{proposed.Name}' ({proposed.Location}) for {operation.Method} {operation.Path}.");
                parameters = [];
                return false;
            }

            var defined = operation.Parameters.FirstOrDefault(parameter =>
                string.Equals(parameter.Name, proposed.Name, StringComparison.Ordinal) &&
                string.Equals(parameter.Location, location, StringComparison.OrdinalIgnoreCase));
            if (defined is null)
            {
                warnings.Add($"Unknown parameter '{proposed.Name}' ({location}) for {operation.Method} {operation.Path}.");
                parameters = [];
                return false;
            }

            accepted.Add(new GeneratedApiParameter(
                defined.Name,
                location,
                defined.Required,
                proposed.Placeholder ?? ""));
        }

        parameters = accepted;
        return true;
    }

    private static bool TryAcceptBody(
        AiTestScenario proposal,
        ImportedOperation operation,
        List<string> warnings,
        out string? body)
    {
        body = null;
        var proposed = proposal.RequestBody;
        if (string.IsNullOrWhiteSpace(proposed))
        {
            return true;
        }

        if (operation.RequestBody is null)
        {
            warnings.Add($"Request body supplied for {operation.Method} {operation.Path} which has no request body.");
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(proposed);
            body = CanonicalJson(document.RootElement);
            return true;
        }
        catch (JsonException)
        {
            warnings.Add($"Request body is not valid JSON for {operation.Method} {operation.Path}.");
            return false;
        }
    }

    private static (int Status, ImportedSchema? Schema) ReconcileResponse(
        AiTestScenario proposal,
        ImportedOperation operation)
    {
        return proposal.Type switch
        {
            AiScenarioType.Negative => SelectNegativeResponse(operation.Responses),
            AiScenarioType.Edge when TryDocumentedStatus(operation.Responses, proposal.ExpectedStatus, out var edge) =>
                edge,
            _ => SelectPositiveResponse(operation.Responses)
        };
    }

    private static bool TryDocumentedStatus(
        IReadOnlyDictionary<string, ImportedSchema?> responses,
        int proposed,
        out (int Status, ImportedSchema? Schema) selected)
    {
        var key = proposed.ToString(CultureInfo.InvariantCulture);
        if (key.Length == 3 && responses.TryGetValue(key, out var schema))
        {
            selected = (proposed, schema);
            return true;
        }

        selected = default;
        return false;
    }

    private static (int Status, ImportedSchema? Schema) SelectPositiveResponse(
        IReadOnlyDictionary<string, ImportedSchema?> responses)
    {
        var numeric = NumericStatuses(responses);
        var twoXx = LowestInRange(numeric, 200, 299);
        if (twoXx is not null)
        {
            return (twoXx.Value.Status, responses[twoXx.Value.Key]);
        }

        if (numeric.Count == 0)
        {
            responses.TryGetValue("default", out var defaultSchema);
            return (ApiTestGenerator.FallbackExpectedStatus, defaultSchema);
        }

        var selected = LowestInRange(numeric, 300, 399)
            ?? LowestInRange(numeric, 400, 499)
            ?? LowestInRange(numeric, 500, 599)
            ?? numeric.OrderBy(item => item.Status).First();
        return (selected.Status, responses[selected.Key]);
    }

    private static (int Status, ImportedSchema? Schema) SelectNegativeResponse(
        IReadOnlyDictionary<string, ImportedSchema?> responses)
    {
        if (responses.ContainsKey("400"))
        {
            return (400, responses["400"]);
        }

        if (responses.ContainsKey("422"))
        {
            return (422, responses["422"]);
        }

        return (400, null);
    }

    private static List<(int Status, string Key)> NumericStatuses(
        IReadOnlyDictionary<string, ImportedSchema?> responses)
    {
        var numeric = new List<(int Status, string Key)>();
        foreach (var key in responses.Keys)
        {
            if (key.Length == 3 && int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var code))
            {
                numeric.Add((code, key));
            }
        }

        return numeric;
    }

    private static (int Status, string Key)? LowestInRange(
        IReadOnlyList<(int Status, string Key)> statuses,
        int minInclusive,
        int maxInclusive)
    {
        (int Status, string Key)? best = null;
        foreach (var item in statuses)
        {
            if (item.Status < minInclusive || item.Status > maxInclusive)
            {
                continue;
            }

            if (best is null || item.Status < best.Value.Status)
            {
                best = item;
            }
        }

        return best;
    }

    private static string ScenarioKey(
        string sourceMethod,
        string sourcePath,
        GeneratedApiTestKind kind,
        string? body,
        IReadOnlyList<GeneratedApiParameter> parameters,
        int expectedStatus)
    {
        var builder = new StringBuilder();
        builder.Append(sourceMethod.Trim().ToUpperInvariant()).Append('\n');
        builder.Append(sourcePath).Append('\n');
        builder.Append(kind).Append('\n');
        builder.Append(body ?? "").Append('\n');
        builder.Append(ParameterFingerprint(parameters)).Append('\n');
        builder.Append(expectedStatus.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static string ExecutableFingerprint(GeneratedApiTestCase test) =>
        string.Join('\n', [
            test.SourceMethod.Trim().ToUpperInvariant(),
            test.SourcePath,
            test.RequestBody ?? "",
            ParameterFingerprint(test.Parameters),
            test.ExpectedStatus.ToString(CultureInfo.InvariantCulture)
        ]);

    private static string ParameterFingerprint(IReadOnlyList<GeneratedApiParameter> parameters) =>
        string.Join(';', parameters
            .OrderBy(parameter => parameter.Location, StringComparer.Ordinal)
            .ThenBy(parameter => parameter.Name, StringComparer.Ordinal)
            .Select(parameter =>
                $"{parameter.Location.ToLowerInvariant()}:{parameter.Name}={parameter.Placeholder}"));

    private static string CanonicalJson(JsonElement element)
    {
        var node = JsonNode.Parse(element.GetRawText());
        return Canonicalize(node)?.ToJsonString(JsonOptions) ?? "";
    }

    private static JsonNode? Canonicalize(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var ordered = new JsonObject();
            foreach (var property in obj.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                ordered[property.Key] = property.Value is null
                    ? null
                    : Canonicalize(JsonNode.Parse(property.Value.ToJsonString()));
            }

            return ordered;
        }

        if (node is JsonArray array)
        {
            var copy = new JsonArray();
            foreach (var item in array)
            {
                copy.Add(item is null ? null : Canonicalize(JsonNode.Parse(item.ToJsonString())));
            }

            return copy;
        }

        return node is null ? null : JsonNode.Parse(node.ToJsonString());
    }

    private static string SubstitutePath(string pathTemplate, IReadOnlyList<GeneratedApiParameter> parameters)
    {
        var path = pathTemplate;
        foreach (var parameter in parameters)
        {
            if (!string.Equals(parameter.Location, "path", StringComparison.Ordinal))
            {
                continue;
            }

            path = path.Replace($"{{{parameter.Name}}}", parameter.Placeholder, StringComparison.Ordinal);
        }

        return path;
    }

    private static GeneratedApiTestKind MapKind(AiScenarioType type) => type switch
    {
        AiScenarioType.Negative => GeneratedApiTestKind.AiNegative,
        AiScenarioType.Edge => GeneratedApiTestKind.AiEdge,
        _ => GeneratedApiTestKind.AiPositive
    };

    private static OperationIdentity Operation(GeneratedApiTestCase test) =>
        new(test.SourceMethod.ToUpperInvariant(), test.SourcePath);

    private static void Increment(
        Dictionary<OperationIdentity, TypeCounts> counts,
        OperationIdentity identity,
        GeneratedApiTestKind kind)
    {
        var current = counts.GetValueOrDefault(identity);
        counts[identity] = kind switch
        {
            GeneratedApiTestKind.AiNegative => current with { Negative = current.Negative + 1 },
            GeneratedApiTestKind.AiEdge => current with { Edge = current.Edge + 1 },
            _ => current with { Positive = current.Positive + 1 }
        };
    }

    private static int CountFor(TypeCounts counts, GeneratedApiTestKind kind) => kind switch
    {
        GeneratedApiTestKind.AiNegative => counts.Negative,
        GeneratedApiTestKind.AiEdge => counts.Edge,
        _ => counts.Positive
    };

    private readonly record struct OperationIdentity(string Method, string Path);

    private readonly record struct TypeCounts(int Positive, int Negative, int Edge)
    {
        public int Total => Positive + Negative + Edge;
    }
}

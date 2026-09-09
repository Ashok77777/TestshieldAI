using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace TestShieldAI.Engine;

public sealed class OpenApiIngestor : IOpenApiIngestor
{
    public OpenApiImportResult Import(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            throw new InvalidOperationException(
                "OpenAPI specification could not be parsed: specification text is required.");
        }

        var reader = new OpenApiStringReader();
        OpenApiDocument document;
        OpenApiDiagnostic diagnostic;

        try
        {
            document = reader.Read(spec, out diagnostic);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"OpenAPI specification could not be parsed: {ex.Message}", ex);
        }

        if (diagnostic.Errors.Count > 0)
        {
            var details = string.Join("; ", diagnostic.Errors.Select(error => error.Message));
            throw new InvalidOperationException($"OpenAPI specification could not be parsed: {details}");
        }

        if (document.Paths is null || document.Paths.Count == 0)
        {
            return new OpenApiImportResult([]);
        }

        var operations = new List<ImportedOperation>();

        foreach (var (path, pathItem) in document.Paths)
        {
            if (pathItem?.Operations is null)
            {
                continue;
            }

            foreach (var (operationType, operation) in pathItem.Operations)
            {
                operations.Add(MapOperation(document, path, pathItem, operationType, operation));
            }
        }

        return new OpenApiImportResult(operations);
    }

    private static ImportedOperation MapOperation(
        OpenApiDocument document,
        string path,
        OpenApiPathItem pathItem,
        OperationType operationType,
        OpenApiOperation operation)
    {
        var method = operationType.ToString().ToUpperInvariant();
        var parameters = MergeParameters(document, pathItem, operation);

        var requestBody = operation.RequestBody is null
            ? null
            : ResolveComponent(document, operation.RequestBody, static components => components.RequestBodies, "requestBodies");
        var requestSchema = MapMediaSchema(document, requestBody?.Content);
        var responses = MapResponses(document, operation.Responses);

        return new ImportedOperation(method, path, parameters, requestSchema, responses);
    }

    private static IReadOnlyList<ImportedParameter> MergeParameters(
        OpenApiDocument document,
        OpenApiPathItem pathItem,
        OpenApiOperation operation)
    {
        var merged = new Dictionary<string, ImportedParameter>(StringComparer.Ordinal);

        foreach (var parameter in EnumerateParameters(pathItem.Parameters, document))
        {
            merged[ParameterKey(parameter)] = parameter;
        }

        foreach (var parameter in EnumerateParameters(operation.Parameters, document))
        {
            merged[ParameterKey(parameter)] = parameter;
        }

        return merged.Values.ToList();
    }

    private static IEnumerable<ImportedParameter> EnumerateParameters(
        IList<OpenApiParameter>? parameters,
        OpenApiDocument document)
    {
        if (parameters is null)
        {
            yield break;
        }

        foreach (var parameter in parameters)
        {
            if (parameter is null)
            {
                continue;
            }

            var resolved = ResolveComponent(
                document,
                parameter,
                static components => components.Parameters,
                "parameters");

            if (string.IsNullOrWhiteSpace(resolved.Name))
            {
                continue;
            }

            var location = resolved.In?.ToString().ToLowerInvariant() ?? "query";
            var schema = MapSchema(document, resolved.Schema, []);
            yield return new ImportedParameter(resolved.Name, location, resolved.Required, schema);
        }
    }

    private static string ParameterKey(ImportedParameter parameter) =>
        $"{parameter.Location}:{parameter.Name}";

    private static IReadOnlyDictionary<string, ImportedSchema?> MapResponses(
        OpenApiDocument document,
        OpenApiResponses? responses)
    {
        if (responses is null || responses.Count == 0)
        {
            return new Dictionary<string, ImportedSchema?>();
        }

        var mapped = new Dictionary<string, ImportedSchema?>(StringComparer.Ordinal);

        foreach (var (statusCode, response) in responses)
        {
            if (response is null)
            {
                mapped[statusCode] = null;
                continue;
            }

            var resolved = ResolveComponent(
                document,
                response,
                static components => components.Responses,
                "responses");

            mapped[statusCode] = MapMediaSchema(document, resolved.Content);
        }

        return mapped;
    }

    private static ImportedSchema? MapMediaSchema(
        OpenApiDocument document,
        IDictionary<string, OpenApiMediaType>? content)
    {
        if (content is null || content.Count == 0)
        {
            return null;
        }

        if (!content.TryGetValue("application/json", out var mediaType))
        {
            mediaType = content.Values.First();
        }

        return MapSchema(document, mediaType.Schema, []);
    }

    private static ImportedSchema? MapSchema(
        OpenApiDocument document,
        OpenApiSchema? schema,
        HashSet<OpenApiSchema> visiting)
    {
        if (schema is null)
        {
            return null;
        }

        schema = ResolveComponent(document, schema, static components => components.Schemas, "schemas");

        if (!visiting.Add(schema))
        {
            return new ImportedSchema(schema.Type, [], new Dictionary<string, ImportedSchema>(), [], null);
        }

        try
        {
            var required = schema.Required is { Count: > 0 }
                ? schema.Required.ToList()
                : [];

            var properties = new Dictionary<string, ImportedSchema>(StringComparer.Ordinal);
            if (schema.Properties is not null)
            {
                foreach (var (name, propertySchema) in schema.Properties)
                {
                    var mapped = MapSchema(document, propertySchema, visiting);
                    if (mapped is not null)
                    {
                        properties[name] = mapped;
                    }
                }
            }

            var enumValues = MapEnum(schema.Enum);
            var items = schema.Items is null ? null : MapSchema(document, schema.Items, visiting);

            return new ImportedSchema(schema.Type, required, properties, enumValues, items);
        }
        finally
        {
            visiting.Remove(schema);
        }
    }

    private static T ResolveComponent<T>(
        OpenApiDocument document,
        T item,
        Func<OpenApiComponents, IDictionary<string, T>?> select,
        string componentType,
        HashSet<string>? resolving = null)
        where T : class, IOpenApiReferenceable
    {
        if (item.Reference is null || string.IsNullOrWhiteSpace(item.Reference.Id))
        {
            return item;
        }

        var id = item.Reference.Id;
        var map = document.Components is null ? null : select(document.Components);
        if (map is null || !map.TryGetValue(id, out var resolved) || resolved is null)
        {
            throw new InvalidOperationException(
                $"OpenAPI $ref could not be resolved: #/components/{componentType}/{id}");
        }

        // Component entries often keep a self-reference to their own id.
        if (ReferenceEquals(item, resolved) ||
            resolved.Reference is null ||
            string.Equals(resolved.Reference.Id, id, StringComparison.Ordinal))
        {
            return resolved;
        }

        resolving ??= new HashSet<string>(StringComparer.Ordinal);
        var key = $"{componentType}:{id}";
        if (!resolving.Add(key))
        {
            throw new InvalidOperationException(
                $"OpenAPI $ref could not be resolved: cyclic reference #/components/{componentType}/{id}");
        }

        try
        {
            return ResolveComponent(document, resolved, select, componentType, resolving);
        }
        finally
        {
            resolving.Remove(key);
        }
    }

    private static IReadOnlyList<string> MapEnum(IList<IOpenApiAny>? values)
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        var mapped = new List<string>();
        foreach (var value in values)
        {
            var text = value switch
            {
                OpenApiString s => s.Value,
                OpenApiInteger i => i.Value.ToString(),
                OpenApiLong l => l.Value.ToString(),
                OpenApiBoolean b => b.Value ? "true" : "false",
                OpenApiNull => null,
                _ => value?.ToString()
            };

            if (!string.IsNullOrEmpty(text))
            {
                mapped.Add(text);
            }
        }

        return mapped;
    }
}

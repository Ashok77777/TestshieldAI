using TestShieldAI.Api.Contracts;
using TestShieldAI.Api.Persistence;
using TestShieldAI.Engine;

namespace TestShieldAI.Api.Endpoints;

public static class SpecsImportEndpoint
{
    public const string Route = "/api/projects/{projectId}/specs/import";

    public static string RouteFor(Guid projectId) => $"/api/projects/{projectId}/specs/import";

    public static IEndpointRouteBuilder MapSpecsImport(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Route, Import)
            .WithName("ImportSpec")
            .WithOpenApi();

        return endpoints;
    }

    private static async Task<IResult> Import(
        Guid projectId,
        ImportSpecRequest? request,
        IProjectStore projects,
        IOpenApiIngestor ingestor,
        IOpenApiSpecificationStore store,
        CancellationToken cancellationToken)
    {
        var project = await projects.FindByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return Results.Problem(
                title: "Project not found",
                detail: $"No project exists with id '{projectId}'.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (string.IsNullOrWhiteSpace(request?.Specification))
        {
            return InvalidSpecificationProblem(
                "OpenAPI specification could not be parsed: specification text is required.");
        }

        try
        {
            var specification = request.Specification;
            var result = ingestor.Import(specification);
            await store.SaveRawSpecificationAsync(project.Id, specification, cancellationToken);
            return Results.Ok(ImportSpecMapper.ToResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return InvalidSpecificationProblem(ex.Message);
        }
    }

    private static IResult InvalidSpecificationProblem(string detail) =>
        Results.Problem(
            title: "Invalid OpenAPI specification",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);
}

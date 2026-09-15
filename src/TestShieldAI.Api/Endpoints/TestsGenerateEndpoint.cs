using TestShieldAI.Api.Contracts;
using TestShieldAI.Api.OpenApi;
using TestShieldAI.Api.Persistence;
using TestShieldAI.Api.TestsRun;
using TestShieldAI.Engine;

namespace TestShieldAI.Api.Endpoints;

public static class TestsGenerateEndpoint
{
    public const string Route = "/api/projects/{projectId}/tests/generate";

    public static string RouteFor(Guid projectId) => $"/api/projects/{projectId}/tests/generate";

    public static IEndpointRouteBuilder MapTestsGenerate(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Route, Generate)
            .WithName("GenerateTests")
            .WithOpenApi();

        return endpoints;
    }

    private static async Task<IResult> Generate(
        Guid projectId,
        IProjectStore projects,
        IOpenApiSpecificationStore specifications,
        IOpenApiIngestor ingestor,
        TestsRunOrchestrator orchestrator,
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

        var records = await specifications.ListByProjectIdAsync(project.Id, cancellationToken);
        var current = CurrentOpenApiSpecificationSelector.SelectLatestPerSpecKey(records, ingestor);
        if (current.Count == 0)
        {
            return Results.Problem(
                title: "OpenAPI specification not found",
                detail: $"No OpenAPI specification has been imported for project '{projectId}'.",
                statusCode: StatusCodes.Status404NotFound);
        }

        try
        {
            var built = await orchestrator.BuildAsync(current, cancellationToken);
            return Results.Ok(GeneratedTestsMapper.ToResponse(built.Tests, built.Coverage, built.Risk));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(
                title: "Invalid OpenAPI specification",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}

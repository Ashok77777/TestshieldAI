using TestShieldAI.Api.Contracts;
using TestShieldAI.Api.OpenApi;
using TestShieldAI.Api.Persistence;
using TestShieldAI.Api.TestsRun;
using TestShieldAI.Engine;

namespace TestShieldAI.Api.Endpoints;

public static class TestsRunEndpoint
{
    public const string Route = "/api/projects/{projectId}/tests/run";

    public static string RouteFor(Guid projectId) => $"/api/projects/{projectId}/tests/run";

    public static IEndpointRouteBuilder MapTestsRun(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Route, Run)
            .WithName("RunTests")
            .WithOpenApi();

        return endpoints;
    }

    private static async Task<IResult> Run(
        Guid projectId,
        IProjectStore projects,
        IOpenApiSpecificationStore specifications,
        IOpenApiIngestor ingestor,
        TestsRunOrchestrator orchestrator,
        IProjectBaselineStore baselines,
        IProjectTestRunStore lastRuns,
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
            var existingBaseline = await baselines.FindByProjectIdAsync(project.Id, cancellationToken);
            var baselineSnapshots = existingBaseline is null
                ? []
                : RegressionPersistenceJson.DeserializeSnapshots(existingBaseline.SnapshotJson);

            var execution = await orchestrator.ExecuteAsync(
                project.BaseUrl,
                current,
                baselineSnapshots,
                cancellationToken);

            await lastRuns.SaveAsync(
                project.Id,
                RunTestsMapper.ToLastRunSave(execution.Results, execution.Detection),
                cancellationToken);

            DateTimeOffset? baselineEstablishedAt = existingBaseline?.EstablishedAt;
            if (execution.PromoteBaseline)
            {
                await baselines.SaveAsync(
                    project.Id,
                    execution.BaselineSnapshotsToSave,
                    cancellationToken: cancellationToken);
                baselineEstablishedAt = (await baselines.FindByProjectIdAsync(project.Id, cancellationToken))
                    ?.EstablishedAt;
            }

            return Results.Ok(
                RunTestsMapper.ToResponse(
                    project.BaseUrl,
                    execution.Results,
                    execution.Detection,
                    baselineEstablishedAt,
                    execution.Ai,
                    execution.Coverage,
                    execution.Risk));
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

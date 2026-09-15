using TestShieldAI.Api.Contracts;
using TestShieldAI.Api.Persistence;

namespace TestShieldAI.Api.Endpoints;

public static class ProjectsEndpoint
{
    public const string Route = "/api/projects";

    public static IEndpointRouteBuilder MapProjects(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Route, Create)
            .WithName("CreateProject")
            .WithOpenApi();

        endpoints.MapGet(Route, List)
            .WithName("ListProjects")
            .WithOpenApi();

        return endpoints;
    }

    private static async Task<IResult> Create(
        CreateProjectRequest? request,
        IProjectStore store,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Name) || string.IsNullOrWhiteSpace(request.BaseUrl))
        {
            return Results.Problem(
                title: "Invalid project",
                detail: "Name and baseUrl are required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var project = await store.CreateAsync(request.Name.Trim(), request.BaseUrl.Trim(), cancellationToken);
        var body = ToResponse(project);
        return Results.Created($"{Route}/{project.Id}", body);
    }

    private static async Task<IResult> List(IProjectStore store, CancellationToken cancellationToken)
    {
        var projects = await store.ListAsync(cancellationToken);
        return Results.Ok(projects.Select(ToResponse).ToList());
    }

    private static ProjectResponse ToResponse(ProjectRecord project) =>
        new()
        {
            Id = project.Id,
            Name = project.Name,
            BaseUrl = project.BaseUrl,
            CreatedAt = project.CreatedAt
        };
}

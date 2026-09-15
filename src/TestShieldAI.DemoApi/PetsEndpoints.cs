namespace TestShieldAI.DemoApi;

public static class PetsEndpoints
{
    public static IEndpointRouteBuilder MapPets(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/pets", List)
            .WithName("ListPets")
            .WithTags("Pets")
            .Produces<IReadOnlyList<Pet>>(StatusCodes.Status200OK)
            .WithOpenApi(operation =>
            {
                operation.Summary = "List pets";
                operation.Description = "Returns all pets in the in-memory store.";
                return operation;
            });

        endpoints.MapGet("/pets/{id:int}", GetById)
            .WithName("GetPetById")
            .WithTags("Pets")
            .Produces<Pet>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Get a pet by id";
                var id = operation.Parameters[0];
                id.Required = true;
                id.Description = "Pet identifier.";
                return operation;
            });

        endpoints.MapPost("/pets", Create)
            .WithName("CreatePet")
            .WithTags("Pets")
            .Accepts<CreatePetRequest>("application/json")
            .Produces<Pet>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .WithOpenApi(operation =>
            {
                operation.Summary = "Create a pet";
                return operation;
            });

        return endpoints;
    }

    private static IResult List(PetStore store) => Results.Ok(store.List());

    private static IResult GetById(int id, PetStore store)
    {
        var pet = store.Find(id);
        return pet is null ? Results.NotFound() : Results.Ok(pet);
    }

    private static IResult Create(CreatePetRequest? request, PetStore store)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var pet = store.Add(request!.Name!, request.Species!);
        return Results.Created($"/pets/{pet.Id}", pet);
    }

    private static Dictionary<string, string[]> Validate(CreatePetRequest? request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (request is null)
        {
            errors["request"] = ["A JSON body with name and species is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["The name field is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Species))
        {
            errors["species"] = ["The species field is required."];
        }

        return errors;
    }
}

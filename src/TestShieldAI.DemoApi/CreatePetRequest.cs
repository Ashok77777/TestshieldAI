using System.ComponentModel.DataAnnotations;

namespace TestShieldAI.DemoApi;

public sealed class CreatePetRequest
{
    [Required]
    [MinLength(1)]
    public string? Name { get; set; }

    [Required]
    [MinLength(1)]
    public string? Species { get; set; }
}

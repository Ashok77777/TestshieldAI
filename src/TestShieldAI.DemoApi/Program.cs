using TestShieldAI.DemoApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TestShield Demo Pets API",
        Version = "v1",
        Description = "Local target API for TestShield AI demos. This is not part of TestShield."
    });
});
builder.Services.AddSingleton<PetStore>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapPets();

app.Run();

public partial class Program;

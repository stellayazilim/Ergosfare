using Stella.Ergosfare.E2E.Api;
using Stella.Ergosfare.E2E.Api.Contracts;
using Stella.Ergosfare.E2E.Api.Endpoints;
using Stella.Ergosfare.E2E.Infrastructure;
using Stella.Ergosfare.E2E.UseCases;
using Stella.MinimalApi.Extensions;

// The slim builder: the AOT-friendly host, without the hosting features this app does not
// use and the trimmer would otherwise have to keep.
var builder = WebApplication.CreateSlimBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Todo") ?? "Data Source=e2e-todos.db";

// The application layer owns the composition — including the generator that emits
// RegisterGenerated() and the plugin it installs. This host holds no Ergosfare reference of
// its own; it reaches the mediator contracts transitively and dispatches through them.
builder.Services.AddApplication();

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddSingleton<TodoStats>();

// Endpoint discovery, also at compile time — see GeneratedEndpointDiscovery.
builder.Services.AddEndpoints(new GeneratedEndpointDiscovery());

// Serializers likewise: the source-generated context goes first in the resolver chain, so
// nothing on the wire needs reflection to be written.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default));

builder.Services.AddExceptionHandler<TodoExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// A clean database on every boot so the .http assertions start from a known-empty state.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<TodoStore>().ResetSchemaAsync();
}

app.UseExceptionHandler();
app.MapEndpoints();
app.Run();

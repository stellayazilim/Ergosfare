using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.E2E.Api;
using Stella.Ergosfare.E2E.Api.Contracts;
using Stella.Ergosfare.E2E.Api.Endpoints;
using Stella.Ergosfare.E2E.Infrastructure;
using Stella.Ergosfare.E2E.UseCases;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Stella.MinimalApi.Extensions;

// The slim builder: the AOT-friendly host, without the hosting features this app does not
// use and the trimmer would otherwise have to keep.
var builder = WebApplication.CreateSlimBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Todo") ?? "Data Source=e2e-todos.db";

// Source-generated registration. RegisterGenerated() is emitted into this compilation by the
// analyzer; it discovers the handlers and the interceptor over in the UseCases assembly.
builder.Services.AddErgosfare(o => o
    .AddCommandModule(c => c.RegisterGenerated())
    .AddQueryModule(q => q.RegisterGenerated())
    .AddEventModule(e => e.RegisterGenerated()));

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

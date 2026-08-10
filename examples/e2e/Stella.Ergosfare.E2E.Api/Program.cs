using System.Reflection;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.E2E.Api;
using Stella.Ergosfare.E2E.Infrastructure;
using Stella.Ergosfare.E2E.UseCases;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Stella.MinimalApi.Extensions;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Todo") ?? "Data Source=e2e-todos.db";

// Source-generated registration. RegisterGenerated() is emitted into this compilation by the
// analyzer; it discovers the handlers and the interceptor over in the UseCases assembly.
builder.Services.AddErgosfare(o => o
    .AddCommandModule(c => c.RegisterGenerated())
    .AddQueryModule(q => q.RegisterGenerated())
    .AddEventModule(e => e.RegisterGenerated()));

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddSingleton<TodoStats>();
builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());
builder.Services.AddExceptionHandler<TodoExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// A clean database on every boot so the .http assertions start from a known-empty state.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
    db.Database.EnsureDeleted();
    db.Database.EnsureCreated();
}

app.UseExceptionHandler();
app.MapEndpoints();
app.Run();

using System.Reflection;
using Stella.MinimalApi.Extensions;
using TodoExample.Data;
using TodoExample.UseCases.Queries;
using TodosExample.UseCases.Commands;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services
    .AddQueries()
    .AddCommands()
    .AddData()
    .AddOpenApi();

var app = builder.Build();
app.MapEndpoints();
app.Run();


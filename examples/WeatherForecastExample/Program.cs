using System.Reflection;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Stella.MinimalApi.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddEndpoints(Assembly.GetExecutingAssembly())
    .AddErgosfare(o => o.AddQueryModule(q => 
        q.RegisterFromAssembly(Assembly.GetExecutingAssembly())))
    .AddOpenApi();

var app = builder.Build();

app.MapEndpoints();
app.Run();


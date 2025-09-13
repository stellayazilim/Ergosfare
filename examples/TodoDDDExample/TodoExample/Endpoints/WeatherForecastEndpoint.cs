using Ergosfare.Queries.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Stella.MinimalApi;
using TodoExample.Contracts.Queries;

namespace TodoExample.Endpoints;

public class WeatherForecastEndpoint: IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/weatherforecast/{amount}", ([FromRoute]byte amount, IQueryMediator mediator) => mediator.QueryAsync(new WeatherForecastQuery(amount)) ).WithName("GetWeatherForecast");

    }
}
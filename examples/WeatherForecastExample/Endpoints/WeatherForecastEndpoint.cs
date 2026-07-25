using Ergosfare.Queries.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Stella.MinimalApi;
using WeatherForecastExample.Queries;

namespace WeatherForecastExample.Endpoints;

public class WeatherForecastEndpoint(IQueryMediator queryMediator):IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/weatherforecast/{amount}", ([FromRoute]byte amount) => 
            queryMediator.QueryAsync(new WeatherForecastQuery(amount)))
            .WithName("GetWeatherForecast");
    }
}
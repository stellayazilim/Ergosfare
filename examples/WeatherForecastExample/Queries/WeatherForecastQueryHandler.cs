using Ergosfare.Context;
using Ergosfare.Contracts;
using WeatherForecastExample.Entities;

namespace WeatherForecastExample.Queries;

public class WeatherForecastQueryHandler: IQueryHandler<WeatherForecastQuery, WeatherForecast[]>
{
    
    private readonly string[] _summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];
    
    public Task<WeatherForecast[]> HandleAsync(WeatherForecastQuery query, IExecutionContext context)
    {
        var foreCasts =  Enumerable.Range(1, query.Amount).Select(index =>
            new WeatherForecast
            (
                DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                Random.Shared.Next(-20, 55),
                _summaries[Random.Shared.Next(_summaries.Length)]
            )).ToArray();

        return Task.FromResult(foreCasts);
    }
}
using Ergosfare.Context;
using Ergosfare.Contracts;
using TodoExample.Contracts.Queries;

namespace TodoExample.UseCases.Queries.WeatherForecast;

public class WeatherForecastQueryHandler: IQueryHandler<WeatherForecastQuery, Domain.WeatherForecast[]>
{
    private readonly string[] _summaries =
        ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

    public Task<Domain.WeatherForecast[]> HandleAsync(WeatherForecastQuery query, IExecutionContext context)
    {
        var forecast = Enumerable.Range(1, query.Amount).Select(index =>
                new Domain.WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    _summaries[Random.Shared.Next(_summaries.Length)]
                ))
            .ToArray();
        return Task.FromResult(forecast);
    }
}
using Ergosfare.Contracts;
using WeatherForecastExample.Entities;

namespace WeatherForecastExample.Queries;

public record WeatherForecastQuery(byte Amount) : IQuery<WeatherForecast[]>;
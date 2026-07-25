using Ergosfare.Contracts;
using TodoExample.Domain;

namespace TodoExample.Contracts.Queries;

public record WeatherForecastQuery(byte Amount): IQuery<WeatherForecast[]>;
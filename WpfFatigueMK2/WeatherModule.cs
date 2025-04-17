using System;

public class WorldWeatherResult
{
    public WeatherData data { get; set; }
}

public class WeatherData
{
    public List<Request>? request { get; set; }
    public List<CurrentCondition>? current_condition { get; set; }
    public List<WeatherDay>? weather { get; set; }
}

public class Request
{
    public string? type { get; set; }
    public string? query { get; set; }
}

public class CurrentCondition
{
    public string? temp_C { get; set; }
    public string? temp_F { get; set; }
    public List<WeatherDescription>? weatherDesc { get; set; }
    public List<WeatherIconUrl>? weatherIconUrl { get; set; }
    public string? humidity { get; set; }
    public string? windspeedKmph { get; set; }
    public string? precipMM { get; set; }
}

public class WeatherDay
{
    public string? date { get; set; }
    public List<Hourly>? hourly { get; set; }
}

public class Hourly
{
    public string? time { get; set; }
    public string? tempC { get; set; }
    public string? tempF { get; set; }
    public List<WeatherDescription>? weatherDesc { get; set; }
    public List<WeatherIconUrl>? weatherIconUrl { get; set; }
    public string? humidity { get; set; }
    public string? windspeedKmph { get; set; }
    public string? precipMM { get; set; }
}

public class WeatherDescription
{
    public string? value { get; set; }
}

public class WeatherIconUrl
{
    public string? value { get; set; }
}



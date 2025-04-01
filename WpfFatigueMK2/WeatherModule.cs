using System;

public class WeatherResult
{
    public List<Forecast> list { get; set; }
}

public class Forecast
{
    public Main main { get; set; }
    public List<Weather> weather { get; set; }
}

public class Main
{
    public double temp { get; set; }
}

public class Weather
{
    public string main { get; set; }
    public string description { get; set; }
}


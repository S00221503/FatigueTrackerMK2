using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

public class WeatherService
{
    public static async Task<WorldWeatherResult> GetForecastAsync(string place)
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"https://world-weather-online-api1.p.rapidapi.com/weather.ashx?q={Uri.EscapeDataString(place)}&num_of_days=1&tp=1&format=json"),
            Headers =
            {
                { "x-rapidapi-key", "5a6c86544dmsh82da401975a8a25p168327jsn48cf9883c89b" },
                { "x-rapidapi-host", "world-weather-online-api1.p.rapidapi.com" },
                { "Accept", "application/json" },
            },
        };

        var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<WorldWeatherResult>(json);
        }
        return null;
    }
}


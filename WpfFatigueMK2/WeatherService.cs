using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

public class WeatherService
{
    public static async Task<WeatherResult> GetForecastAsync(string place)
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"https://weather-api167.p.rapidapi.com/api/weather/forecast?place={Uri.EscapeDataString(place)}&cnt=1&units=metric&type=three_hour&mode=json&lang=en"),
            Headers =
            {
                { "x-rapidapi-key", "5a6c86544dmsh82da401975a8a25p168327jsn48cf9883c89b" },
                { "x-rapidapi-host", "weather-api167.p.rapidapi.com" },
                { "Accept", "application/json" },
            },
        };

        var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<WeatherResult>(json);
        }
        return null;
    }
}

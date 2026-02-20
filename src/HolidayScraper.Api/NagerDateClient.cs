using System.Text.Json;
using System.Text.Json.Serialization;

namespace HolidayScraper.Api;

public class NagerDateHoliday
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("localName")]
    public string LocalName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("countryCode")]
    public string CountryCode { get; set; } = string.Empty;

    [JsonPropertyName("fixed")]
    public bool Fixed { get; set; }

    [JsonPropertyName("global")]
    public bool Global { get; set; }

    [JsonPropertyName("types")]
    public List<string>? Types { get; set; }
}

public class NagerDateClient
{
    private const string BaseUrl = "https://date.nager.at/api/v3/";
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public NagerDateClient(HttpClient http)
    {
        _http = http;
        _http.BaseAddress = new Uri(BaseUrl);
        _http.DefaultRequestHeaders.Add("User-Agent", "HolidayMonitor-Scraper/1.0");
    }

    public async Task<List<NagerDateHoliday>> GetPublicHolidaysAsync(int year, string countryCode, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"PublicHolidays/{year}/{countryCode}", ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        var list = JsonSerializer.Deserialize<List<NagerDateHoliday>>(json, JsonOptions);
        return list ?? new List<NagerDateHoliday>();
    }

    public async Task<List<string>> GetAvailableCountriesAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("AvailableCountries", ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        var list = JsonSerializer.Deserialize<List<CountryEntry>>(json, JsonOptions);
        return list?.Select(c => c.CountryCode ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList() ?? new List<string>();
    }

    private class CountryEntry
    {
        [JsonPropertyName("countryCode")]
        public string? CountryCode { get; set; }
    }
}

namespace HolidayMonitor.Contracts;

/// <summary>
/// Published when the scraper detects a public holiday (e.g. today) for a country.
/// </summary>
public class PublicHolidayDetected : IEvent
{
    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string LocalName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Fixed { get; set; }
    public bool Global { get; set; }
    public string? Type { get; set; }
    public DateTime DetectedAtUtc { get; set; }
}

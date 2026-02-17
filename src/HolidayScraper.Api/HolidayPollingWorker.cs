using System.Diagnostics;
using HolidayMonitor.Contracts;
using NServiceBus;

namespace HolidayScraper.Api;

public class HolidayPollingWorker : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("HolidayScraper");
    private readonly NagerDateClient _nager;
    private readonly IMessageSession _messageSession;
    private readonly ILogger<HolidayPollingWorker> _logger;
    private readonly IConfiguration _config;
    private readonly TimeSpan _interval;

    public HolidayPollingWorker(
        NagerDateClient nager,
        IMessageSession messageSession,
        ILogger<HolidayPollingWorker> logger,
        IConfiguration config)
    {
        _nager = nager;
        _messageSession = messageSession;
        _logger = logger;
        _config = config;
        _interval = TimeSpan.FromHours(_config.GetValue("PollingIntervalHours", 1));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Holiday scraper started. Polling every {Interval}", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var activity = ActivitySource.StartActivity("PollPublicHolidays");
            try
            {
                await PollAndPublishAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during holiday poll");
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task PollAndPublishAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var year = today.Year;
        var lookAheadDays = _config.GetValue("LookAheadDays", 0); // Default: 0 (only today), set to 7 for next week

        var countryCodes = _config.GetSection("CountriesToMonitor").Get<List<string>>()
            ?? new List<string> { "US", "GB", "DE", "FR", "CA", "AU" };

        // Build list of dates to check (today + next N days)
        var datesToCheck = new List<DateTime>();
        for (int i = 0; i <= lookAheadDays; i++)
        {
            datesToCheck.Add(today.AddDays(i));
        }

        foreach (var countryCode in countryCodes)
        {
            ct.ThrowIfCancellationRequested();
            List<NagerDateHoliday> holidays;
            try
            {
                holidays = await _nager.GetPublicHolidaysAsync(year, countryCode, ct);
                // Also fetch next year if we're looking ahead and near year end
                if (lookAheadDays > 0 && today.Month == 12 && today.Day > (31 - lookAheadDays))
                {
                    var nextYearHolidays = await _nager.GetPublicHolidaysAsync(year + 1, countryCode, ct);
                    holidays.AddRange(nextYearHolidays);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch holidays for {Country}", countryCode);
                continue;
            }

            // Check each date in the look-ahead window
            foreach (var checkDate in datesToCheck)
            {
                var checkDateStr = checkDate.ToString("yyyy-MM-dd");
                var matchingHolidays = holidays.Where(h => string.Equals(h.Date, checkDateStr, StringComparison.Ordinal)).ToList();

                foreach (var h in matchingHolidays)
                {
                    var holidayDate = DateTime.Parse(h.Date);
                    var evt = new PublicHolidayDetected
                    {
                        CountryCode = h.CountryCode,
                        CountryName = CountryName(h.CountryCode),
                        Date = holidayDate,
                        LocalName = h.LocalName,
                        Name = h.Name,
                        Fixed = h.Fixed,
                        Global = h.Global,
                        Type = h.Types?.FirstOrDefault(),
                        DetectedAtUtc = DateTime.UtcNow
                    };
                    await _messageSession.Publish(evt, ct);
                    var daysUntil = (holidayDate - today).Days;
                    var dayLabel = daysUntil == 0 ? "today" : daysUntil == 1 ? "tomorrow" : $"in {daysUntil} days";
                    _logger.LogInformation("Published PublicHolidayDetected: {Country} - {Name} ({Date}) - {DayLabel}",
                        evt.CountryCode, evt.Name, evt.Date, dayLabel);
                }
            }
        }
    }

    private static string CountryName(string code)
    {
        return code switch
        {
            "US" => "United States",
            "GB" => "United Kingdom",
            "DE" => "Germany",
            "FR" => "France",
            "CA" => "Canada",
            "AU" => "Australia",
            _ => code
        };
    }
}

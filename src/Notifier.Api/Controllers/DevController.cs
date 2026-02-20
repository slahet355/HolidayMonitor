using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Notifier.Api.Hubs;

namespace Notifier.Api.Controllers;

[ApiController]
[Route("api/dev")]
public class DevController : ControllerBase
{
    private readonly IHubContext<NotificationHub> _hub;

    public DevController(IHubContext<NotificationHub> hub)
    {
        _hub = hub;
    }

    public class DevNotifyDto
    {
        public List<string>? UserIds { get; set; } = new();
        public string? CountryCode { get; set; }
        public string? CountryName { get; set; }
        public DateTime Date { get; set; }
        public string? LocalName { get; set; }
        public string? Name { get; set; }
        public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;
    }

    [HttpPost("notify")]
    public async Task<IActionResult> Notify([FromBody] DevNotifyDto dto)
    {
        var payload = new
        {
            type = "HolidayDetected",
            countryCode = dto.CountryCode,
            countryName = dto.CountryName,
            date = dto.Date,
            localName = dto.LocalName,
            name = dto.Name,
            detectedAtUtc = dto.DetectedAtUtc
        };

        foreach (var userId in dto.UserIds ?? Enumerable.Empty<string>())
        {
            await _hub.Clients.Group(userId).SendAsync("HolidayDetected", payload);
        }

        return Ok();
    }
}

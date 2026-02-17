using Microsoft.AspNetCore.Mvc;
using UserPref.Api.Models;
using UserPref.Api.Repositories;

namespace UserPref.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionRepository _repo;
    private readonly ILogger<SubscriptionsController> _logger;

    public SubscriptionsController(ISubscriptionRepository repo, ILogger<SubscriptionsController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<SubscriptionDto>> Get(string userId, CancellationToken ct)
    {
        var sub = await _repo.GetByUserIdAsync(userId, ct);
        if (sub == null)
            return Ok(new SubscriptionDto { UserId = userId, CountryCodes = new List<string>() });
        return Ok(new SubscriptionDto { UserId = sub.UserId, CountryCodes = sub.CountryCodes });
    }

    [HttpPut("{userId}")]
    public async Task<ActionResult<SubscriptionDto>> Put(string userId, [FromBody] SubscriptionDto dto, CancellationToken ct)
    {
        if (dto.UserId != null && dto.UserId != userId)
            return BadRequest("UserId in body must match route.");
        var sub = await _repo.UpsertAsync(userId, dto.CountryCodes ?? new List<string>(), ct);
        return Ok(new SubscriptionDto { UserId = sub.UserId, CountryCodes = sub.CountryCodes });
    }
}

public class SubscriptionDto
{
    public string? UserId { get; set; }
    public List<string>? CountryCodes { get; set; }
}

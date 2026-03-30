using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using UserPref.Api.Models;
using UserPref.Api.Repositories;

namespace UserPref.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionRepository _repo;
    private readonly ILogger<SubscriptionsController> _logger;

    public SubscriptionsController(ISubscriptionRepository repo, ILogger<SubscriptionsController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<SubscriptionDto>> Get(CancellationToken ct)
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userId == null) return Unauthorized();

        var sub = await _repo.GetByUserIdAsync(userId, ct);
        return Ok(new SubscriptionDto
        {
            UserId = userId,
            CountryCodes = sub?.CountryCodes ?? new List<string>()
        });
    }

    [HttpPut]
    public async Task<ActionResult<SubscriptionDto>> Put([FromBody] SubscriptionDto dto, CancellationToken ct)
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userId == null) return Unauthorized();

        var sub = await _repo.UpsertAsync(userId, dto.CountryCodes ?? new List<string>(), ct);
        return Ok(new SubscriptionDto { UserId = sub.UserId, CountryCodes = sub.CountryCodes });
    }
}

public class SubscriptionDto
{
    public string? UserId { get; set; }
    public List<string>? CountryCodes { get; set; }
}

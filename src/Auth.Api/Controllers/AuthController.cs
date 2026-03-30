using Auth.Api.Models;
using Auth.Api.Repositories;
using Auth.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Auth.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _tokens;
    private readonly TokenService _tokenService;
    private readonly IConfiguration _config;

    public AuthController(
        IUserRepository users,
        IRefreshTokenRepository tokens,
        TokenService tokenService,
        IConfiguration config)
    {
        _users = users;
        _tokens = tokens;
        _tokenService = tokenService;
        _config = config;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password are required." });

        if (request.Password.Length < 8)
            return BadRequest(new { error = "Password must be at least 8 characters." });

        var existing = await _users.FindByEmailAsync(request.Email, ct);
        if (existing != null)
            return Conflict(new { error = "Email already registered." });

        var user = new User
        {
            Email = request.Email.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow,
        };

        await _users.CreateAsync(user, ct);
        return CreatedAtAction(null, null, await IssueTokens(user, ct));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password are required." });

        var user = await _users.FindByEmailAsync(request.Email, ct);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid credentials." });

        return Ok(await IssueTokens(user, ct));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new { error = "Refresh token is required." });

        var stored = await _tokens.FindAsync(request.RefreshToken, ct);
        if (stored == null || stored.ExpiresAt < DateTime.UtcNow)
        {
            if (stored != null) await _tokens.DeleteAsync(request.RefreshToken, ct);
            return Unauthorized(new { error = "Refresh token expired or invalid." });
        }

        var user = await _users.FindByIdAsync(stored.UserId, ct);
        if (user == null)
            return Unauthorized(new { error = "User not found." });

        // Rotate: delete old refresh token, issue new pair
        await _tokens.DeleteAsync(request.RefreshToken, ct);
        return Ok(await IssueTokens(user, ct));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            await _tokens.DeleteAsync(request.RefreshToken, ct);
        return NoContent();
    }

    private async Task<AuthResponse> IssueTokens(User user, CancellationToken ct)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshTokenValue = _tokenService.GenerateRefreshToken();
        var refreshTokenDays = int.TryParse(_config["Jwt:RefreshTokenDays"], out var d) ? d : 7;

        await _tokens.CreateAsync(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenDays),
            CreatedAt = DateTime.UtcNow,
        }, ct);

        return new AuthResponse(accessToken, refreshTokenValue, user.Id, user.Email);
    }
}

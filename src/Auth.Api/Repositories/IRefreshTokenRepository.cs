using Auth.Api.Models;

namespace Auth.Api.Repositories;

public interface IRefreshTokenRepository
{
    Task CreateAsync(RefreshToken token, CancellationToken ct = default);
    Task<RefreshToken?> FindAsync(string token, CancellationToken ct = default);
    Task DeleteAsync(string token, CancellationToken ct = default);
}

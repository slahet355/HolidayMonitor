using Auth.Api.Models;

namespace Auth.Api.Repositories;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> FindByIdAsync(string id, CancellationToken ct = default);
    Task CreateAsync(User user, CancellationToken ct = default);
}

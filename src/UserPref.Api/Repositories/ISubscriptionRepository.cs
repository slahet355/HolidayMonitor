using UserPref.Api.Models;

namespace UserPref.Api.Repositories;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<Subscription> UpsertAsync(string userId, List<string> countryCodes, CancellationToken ct = default);
    Task<List<string>> GetUserIdsSubscribedToCountryAsync(string countryCode, CancellationToken ct = default);
}

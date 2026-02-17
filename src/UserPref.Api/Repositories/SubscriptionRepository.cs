using MongoDB.Driver;
using UserPref.Api.Models;

namespace UserPref.Api.Repositories;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly IMongoCollection<Subscription> _collection;
    private const string DatabaseName = "HolidayMonitor";
    private const string CollectionName = "subscriptions";

    public SubscriptionRepository(IMongoClient client)
    {
        _collection = client.GetDatabase(DatabaseName).GetCollection<Subscription>(CollectionName);
    }

    public async Task<Subscription?> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        return await _collection.Find(s => s.UserId == userId).FirstOrDefaultAsync(ct);
    }

    public async Task<Subscription> UpsertAsync(string userId, List<string> countryCodes, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var filter = Builders<Subscription>.Filter.Eq(s => s.UserId, userId);
        var update = Builders<Subscription>.Update
            .Set(s => s.CountryCodes, countryCodes)
            .Set(s => s.UpdatedAt, now)
            .SetOnInsert(s => s.CreatedAt, now)
            .SetOnInsert(s => s.UserId, userId);
        var options = new FindOneAndUpdateOptions<Subscription>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };
        return await _collection.FindOneAndUpdateAsync(filter, update, options, ct)
            ?? new Subscription { UserId = userId, CountryCodes = countryCodes, CreatedAt = now, UpdatedAt = now };
    }

    public async Task<List<string>> GetUserIdsSubscribedToCountryAsync(string countryCode, CancellationToken ct = default)
    {
        var filter = Builders<Subscription>.Filter.AnyIn(s => s.CountryCodes, new[] { countryCode });
        var cursor = await _collection.FindAsync(filter, cancellationToken: ct);
        var list = await cursor.ToListAsync(ct);
        return list.Select(s => s.UserId).Distinct().ToList();
    }
}

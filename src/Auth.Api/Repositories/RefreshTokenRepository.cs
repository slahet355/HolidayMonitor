using Auth.Api.Models;
using MongoDB.Driver;

namespace Auth.Api.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IMongoCollection<RefreshToken> _collection;

    public RefreshTokenRepository(IMongoClient client, IConfiguration config)
    {
        var db = client.GetDatabase(config["MongoDB:Database"] ?? "HolidayMonitor");
        _collection = db.GetCollection<RefreshToken>("refreshTokens");
        // TTL index: MongoDB auto-deletes documents once ExpiresAt is in the past
        var ttlIndex = new CreateIndexModel<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(t => t.ExpiresAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero });
        _collection.Indexes.CreateOne(ttlIndex);
    }

    public Task CreateAsync(RefreshToken token, CancellationToken ct = default) =>
        _collection.InsertOneAsync(token, cancellationToken: ct);

    public Task<RefreshToken?> FindAsync(string token, CancellationToken ct = default) =>
        _collection.Find(t => t.Token == token).FirstOrDefaultAsync(ct)!;

    public Task DeleteAsync(string token, CancellationToken ct = default) =>
        _collection.DeleteOneAsync(t => t.Token == token, ct);
}

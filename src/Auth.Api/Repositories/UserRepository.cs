using Auth.Api.Models;
using MongoDB.Driver;

namespace Auth.Api.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _collection;

    public UserRepository(IMongoClient client, IConfiguration config)
    {
        var db = client.GetDatabase(config["MongoDB:Database"] ?? "HolidayMonitor");
        _collection = db.GetCollection<User>("users");
        var indexModel = new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(u => u.Email),
            new CreateIndexOptions { Unique = true });
        _collection.Indexes.CreateOne(indexModel);
    }

    public Task<User?> FindByEmailAsync(string email, CancellationToken ct = default) =>
        _collection.Find(u => u.Email == email.ToLowerInvariant()).FirstOrDefaultAsync(ct)!;

    public Task<User?> FindByIdAsync(string id, CancellationToken ct = default) =>
        _collection.Find(u => u.Id == id).FirstOrDefaultAsync(ct)!;

    public Task CreateAsync(User user, CancellationToken ct = default) =>
        _collection.InsertOneAsync(user, cancellationToken: ct);
}

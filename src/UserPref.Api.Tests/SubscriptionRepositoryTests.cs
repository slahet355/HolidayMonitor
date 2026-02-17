using MongoDB.Driver;
using Moq;
using UserPref.Api.Models;
using UserPref.Api.Repositories;

namespace UserPref.Api.Tests;

[TestFixture]
public class SubscriptionRepositoryTests
{
    private Mock<IMongoClient> _mockClient = null!;
    private Mock<IMongoDatabase> _mockDatabase = null!;
    private Mock<IMongoCollection<Subscription>> _mockCollection = null!;
    private SubscriptionRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _mockClient = new Mock<IMongoClient>();
        _mockDatabase = new Mock<IMongoDatabase>();
        _mockCollection = new Mock<IMongoCollection<Subscription>>();

        _mockClient.Setup(c => c.GetDatabase("HolidayMonitor", null))
            .Returns(_mockDatabase.Object);
        _mockDatabase.Setup(d => d.GetCollection<Subscription>("subscriptions", null))
            .Returns(_mockCollection.Object);

        _repository = new SubscriptionRepository(_mockClient.Object);
    }

    [Test]
    public async Task GetByUserIdAsync_ReturnsSubscription_WhenFound()
    {
        // Arrange
        var userId = "user123";
        var expectedSubscription = new Subscription
        {
            Id = "sub123",
            UserId = userId,
            CountryCodes = new List<string> { "US", "GB" }
        };

        var mockCursor = new Mock<IAsyncCursor<Subscription>>();
        mockCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        mockCursor.Setup(c => c.Current).Returns(new[] { expectedSubscription });

        _mockCollection.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Subscription>>(),
                It.IsAny<FindOptions<Subscription, Subscription>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        // Act
        var result = await _repository.GetByUserIdAsync(userId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.UserId, Is.EqualTo(userId));
        Assert.That(result.CountryCodes, Contains.Item("US"));
        Assert.That(result.CountryCodes, Contains.Item("GB"));
    }

    [Test]
    public async Task GetByUserIdAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        var userId = "nonexistent";

        var mockCursor = new Mock<IAsyncCursor<Subscription>>();
        mockCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockCollection.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Subscription>>(),
                It.IsAny<FindOptions<Subscription, Subscription>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        // Act
        var result = await _repository.GetByUserIdAsync(userId);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task UpsertAsync_CreatesNewSubscription_WhenNotExists()
    {
        // Arrange
        var userId = "newuser";
        var countryCodes = new List<string> { "US", "CA" };

        var mockCursor = new Mock<IAsyncCursor<Subscription>>();
        mockCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockCollection.Setup(c => c.FindOneAndUpdateAsync(
                It.IsAny<FilterDefinition<Subscription>>(),
                It.IsAny<UpdateDefinition<Subscription>>(),
                It.IsAny<FindOneAndUpdateOptions<Subscription>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null!);

        // Act
        var result = await _repository.UpsertAsync(userId, countryCodes);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.UserId, Is.EqualTo(userId));
        Assert.That(result.CountryCodes, Is.EqualTo(countryCodes));
        _mockCollection.Verify(c => c.FindOneAndUpdateAsync(
            It.IsAny<FilterDefinition<Subscription>>(),
            It.IsAny<UpdateDefinition<Subscription>>(),
            It.Is<FindOneAndUpdateOptions<Subscription>>(opts => opts.IsUpsert == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetUserIdsSubscribedToCountryAsync_ReturnsUserIds_WhenFound()
    {
        // Arrange
        var countryCode = "US";
        var subscriptions = new List<Subscription>
        {
            new() { UserId = "user1", CountryCodes = new List<string> { "US", "GB" } },
            new() { UserId = "user2", CountryCodes = new List<string> { "US" } },
            new() { UserId = "user3", CountryCodes = new List<string> { "GB" } }
        };

        var mockCursor = new Mock<IAsyncCursor<Subscription>>();
        mockCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        mockCursor.Setup(c => c.Current).Returns(subscriptions.Where(s => s.CountryCodes.Contains(countryCode)));

        _mockCollection.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Subscription>>(),
                It.IsAny<FindOptions<Subscription, Subscription>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        // Act
        var result = await _repository.GetUserIdsSubscribedToCountryAsync(countryCode);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result, Contains.Item("user1"));
        Assert.That(result, Contains.Item("user2"));
        Assert.That(result, Does.Not.Contain("user3"));
    }
}

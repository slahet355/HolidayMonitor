using System.Net;
using System.Text;
using System.Text.Json;
using HolidayScraper.Api;
using Moq;
using Moq.Protected;

namespace HolidayScraper.Api.Tests;

[TestFixture]
public class NagerDateClientTests
{
    private Mock<HttpMessageHandler> _mockHandler = null!;
    private HttpClient _httpClient = null!;
    private NagerDateClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _mockHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHandler.Object);
        _client = new NagerDateClient(_httpClient);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    [Test]
    public async Task GetPublicHolidaysAsync_ReturnsHolidays_WhenApiReturnsData()
    {
        // Arrange
        var year = 2024;
        var countryCode = "US";
        var expectedHolidays = new List<NagerDateHoliday>
        {
            new() { Date = "2024-01-01", Name = "New Year's Day", CountryCode = "US", LocalName = "New Year's Day", Fixed = true, Global = true },
            new() { Date = "2024-07-04", Name = "Independence Day", CountryCode = "US", LocalName = "Independence Day", Fixed = true, Global = true }
        };
        var json = JsonSerializer.Serialize(expectedHolidays);

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains($"/PublicHolidays/{year}/{countryCode}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _client.GetPublicHolidaysAsync(year, countryCode);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result[0].Name, Is.EqualTo("New Year's Day"));
        Assert.That(result[1].Name, Is.EqualTo("Independence Day"));
    }

    [Test]
    public async Task GetPublicHolidaysAsync_ReturnsEmptyList_WhenApiReturnsNull()
    {
        // Arrange
        var year = 2024;
        var countryCode = "XX";
        var json = "null";

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _client.GetPublicHolidaysAsync(year, countryCode);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void GetPublicHolidaysAsync_ThrowsException_WhenApiReturnsError()
    {
        // Arrange
        var year = 2024;
        var countryCode = "INVALID";

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        // Act & Assert
        Assert.ThrowsAsync<HttpRequestException>(async () =>
            await _client.GetPublicHolidaysAsync(year, countryCode));
    }

    [Test]
    public async Task GetAvailableCountriesAsync_ReturnsCountryCodes_WhenApiReturnsData()
    {
        // Arrange
        var expectedCountries = new List<object>
        {
            new { countryCode = "US" },
            new { countryCode = "GB" },
            new { countryCode = "DE" }
        };
        var json = JsonSerializer.Serialize(expectedCountries);

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains("/AvailableCountries")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _client.GetAvailableCountriesAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(3));
        Assert.That(result, Contains.Item("US"));
        Assert.That(result, Contains.Item("GB"));
        Assert.That(result, Contains.Item("DE"));
    }
}

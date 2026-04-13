using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using QuoteFlow.Core.Pricing;

namespace QuoteFlow.IntegrationTests;

public class QuoteEndpointsTests(QuoteFlowWebApplicationFactory factory)
    : IClassFixture<QuoteFlowWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PostQuotePrice_ValidRequest_Returns200WithResult()
    {
        var request = new QuoteRequest
        {
            OriginCode = "BKK",
            DestinationCode = "BKK",
            Weight = 3,
            BasePrice = 100
        };

        var response = await _client.PostAsJsonAsync("/quotes/price", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<QuoteResultResponse>();
        body.Should().NotBeNull();
        body!.FinalPrice.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PostQuotePrice_WeightTierMatches_OverridesBasePrice()
    {
        var request = new QuoteRequest
        {
            OriginCode = "BKK",
            DestinationCode = "BKK",
            Weight = 3,
            BasePrice = 50
        };

        var response = await _client.PostAsJsonAsync("/quotes/price", request);
        var body = await response.Content.ReadFromJsonAsync<QuoteResultResponse>();

        body!.BasePrice.Should().Be(100);
    }

    [Fact]
    public async Task PostQuoteBulk_ValidRequests_ReturnsAcceptedWithJobId()
    {
        var requests = new List<QuoteRequest>
        {
            new() { OriginCode = "BKK", DestinationCode = "CNX", Weight = 3, BasePrice = 100 },
            new() { OriginCode = "BKK", DestinationCode = "HKT", Weight = 7, BasePrice = 150 }
        };

        var response = await _client.PostAsJsonAsync("/quotes/bulk", requests);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var body = await response.Content.ReadFromJsonAsync<BulkJobResponse>();
        body!.JobId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PostQuoteBulk_EmptyList_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/quotes/bulk", new List<QuoteRequest>());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHealth_Returns200()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private record QuoteResultResponse(decimal BasePrice, decimal FinalPrice, decimal Surcharge, decimal Discount);
    private record BulkJobResponse(Guid JobId);
}

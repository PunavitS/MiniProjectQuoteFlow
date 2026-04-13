using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using QuoteFlow.Core.Pricing;

namespace QuoteFlow.IntegrationTests;

public class JobEndpointsTests(QuoteFlowWebApplicationFactory factory)
    : IClassFixture<QuoteFlowWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetJob_AfterBulkSubmit_ReturnsJobStatus()
    {
        var requests = new List<QuoteRequest>
        {
            new() { OriginCode = "BKK", DestinationCode = "CNX", Weight = 3, BasePrice = 100 }
        };

        var bulkResponse = await _client.PostAsJsonAsync("/quotes/bulk", requests);
        var bulk = await bulkResponse.Content.ReadFromJsonAsync<BulkJobResponse>();

        await Task.Delay(500);

        var jobResponse = await _client.GetAsync($"/jobs/{bulk!.JobId}");
        jobResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await jobResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("totalItems").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GetJob_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/jobs/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private record BulkJobResponse(Guid JobId);
}

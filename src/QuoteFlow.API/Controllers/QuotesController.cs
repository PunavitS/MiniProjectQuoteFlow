using Microsoft.AspNetCore.Mvc;
using QuoteFlow.Application.Jobs;
using QuoteFlow.Application.Pricing;
using QuoteFlow.Core.Pricing;
using QuoteFlow.Infrastructure.Jobs;

namespace QuoteFlow.API.Controllers;

[ApiController]
[Route("quotes")]
[Tags("Quotes")]
public class QuotesController(
    IPricingService pricingService,
    IJobService jobService,
    BulkJobWorker worker) : ControllerBase
{
    [HttpPost("price")]
    [ProducesResponseType<QuoteResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CalculatePrice([FromBody] QuoteRequest request)
    {
        var result = await pricingService.CalculateAsync(request);
        return Ok(result);
    }

    [HttpPost("bulk")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitBulk([FromBody] List<QuoteRequest> requests)
    {
        if (requests.Count == 0)
            return BadRequest(new { error = "Request list cannot be empty" });

        var job = await jobService.CreateJobAsync(requests);
        await worker.EnqueueAsync(job.Id);

        return Accepted($"/jobs/{job.Id}", new { jobId = job.Id });
    }

    [HttpPost("bulk/csv")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitBulkCsv(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "CSV file is required" });

        var requests = new List<QuoteRequest>();
        var errors = new List<string>();

        using var reader = new StreamReader(file.OpenReadStream());
        await reader.ReadLineAsync(); // skip header

        int rowIndex = 1;
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = line.Split(',');
            if (cols.Length < 4)
            {
                errors.Add($"Row {rowIndex}: invalid format");
                rowIndex++;
                continue;
            }

            if (!decimal.TryParse(cols[2].Trim(), out var weight) ||
                !decimal.TryParse(cols[3].Trim(), out var basePrice))
            {
                errors.Add($"Row {rowIndex}: invalid number format");
                rowIndex++;
                continue;
            }

            requests.Add(new QuoteRequest
            {
                OriginCode = cols[0].Trim(),
                DestinationCode = cols[1].Trim(),
                Weight = weight,
                BasePrice = basePrice
            });

            rowIndex++;
        }

        if (requests.Count == 0)
            return BadRequest(new { error = "No valid rows found", details = errors });

        var job = await jobService.CreateJobAsync(requests);
        await worker.EnqueueAsync(job.Id);

        return Accepted($"/jobs/{job.Id}", new
        {
            jobId = job.Id,
            validRows = requests.Count,
            skippedRows = errors.Count,
            errors
        });
    }
}

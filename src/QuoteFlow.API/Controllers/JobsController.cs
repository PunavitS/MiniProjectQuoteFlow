using Microsoft.AspNetCore.Mvc;
using QuoteFlow.Application.Jobs;

namespace QuoteFlow.API.Controllers;

[ApiController]
[Route("jobs")]
[Tags("Jobs")]
public class JobsController(IJobService jobService) : ControllerBase
{
    [HttpGet("{jobId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJob(Guid jobId)
    {
        try
        {
            var (job, items) = await jobService.GetJobAsync(jobId);

            return Ok(new
            {
                job.Id,
                Status = job.Status.ToString(),
                job.TotalItems,
                job.ProcessedItems,
                job.FailedItems,
                job.CreatedAt,
                job.CompletedAt,
                Items = items.Select(i => new
                {
                    i.Id,
                    i.RowIndex,
                    i.OriginCode,
                    i.DestinationCode,
                    i.Weight,
                    i.InputBasePrice,
                    i.BasePrice,
                    i.FinalPrice,
                    i.Discount,
                    i.Surcharge,
                    i.AppliedRules,
                    Status = i.Status.ToString(),
                    i.ErrorMessage,
                    i.ProcessedAt
                })
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Job {jobId} not found" });
        }
    }
}

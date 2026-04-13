using QuoteFlow.Core.Jobs;
using QuoteFlow.Core.Pricing;

namespace QuoteFlow.Application.Jobs;

public class JobService(IJobRepository jobRepository) : IJobService
{
    public async Task<BulkJob> CreateJobAsync(List<QuoteRequest> requests)
    {
        var job = new BulkJob
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Pending,
            TotalItems = requests.Count,
            ProcessedItems = 0,
            FailedItems = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var createdJob = await jobRepository.CreateAsync(job);

        for (int i = 0; i < requests.Count; i++)
        {
            var item = new BulkJobItem
            {
                Id = Guid.NewGuid(),
                JobId = createdJob.Id,
                RowIndex = i,
                OriginCode = requests[i].OriginCode,
                DestinationCode = requests[i].DestinationCode,
                Weight = requests[i].Weight,
                InputBasePrice = requests[i].BasePrice,
                Currency = requests[i].Currency,
                Status = JobItemStatus.Pending
            };
            await jobRepository.CreateItemAsync(item);
        }

        return createdJob;
    }

    public async Task<(BulkJob Job, IEnumerable<BulkJobItem> Items)> GetJobAsync(Guid jobId)
    {
        var job = await jobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Job {jobId} not found");
        var items = await jobRepository.GetItemsByJobIdAsync(jobId);
        return (job, items);
    }
}

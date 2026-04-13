using QuoteFlow.Core.Jobs;
using QuoteFlow.Core.Pricing;

namespace QuoteFlow.Application.Jobs;

public interface IJobService
{
    Task<BulkJob> CreateJobAsync(List<QuoteRequest> requests);
    Task<(BulkJob Job, IEnumerable<BulkJobItem> Items)> GetJobAsync(Guid jobId);
}

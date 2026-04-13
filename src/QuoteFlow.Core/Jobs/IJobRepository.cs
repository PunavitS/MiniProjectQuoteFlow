namespace QuoteFlow.Core.Jobs;

public interface IJobRepository
{
    Task<BulkJob> CreateAsync(BulkJob job);
    Task<BulkJob?> GetByIdAsync(Guid id);
    Task<BulkJob> UpdateAsync(BulkJob job);
    Task<IEnumerable<BulkJobItem>> GetItemsByJobIdAsync(Guid jobId);
    Task<BulkJobItem> CreateItemAsync(BulkJobItem item);
    Task<BulkJobItem> UpdateItemAsync(BulkJobItem item);
}

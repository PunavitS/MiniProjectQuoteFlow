namespace QuoteFlow.Core.Jobs;

public class BulkJob
{
    public Guid Id { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public int TotalItems { get; set; }
    public int ProcessedItems { get; set; }
    public int FailedItems { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

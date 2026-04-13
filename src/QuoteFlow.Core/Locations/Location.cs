namespace QuoteFlow.Core.Locations;

public class Location
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public Region Region { get; set; }
    public decimal DistanceFromBkk { get; set; }
    public bool IsRemoteArea { get; set; }
    public bool IsActive { get; set; } = true;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

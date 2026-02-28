namespace AccommodationService.Api;

public class AccommodationResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public List<string> Amenities { get; set; } = new();
    public List<string> Pictures { get; set; } = new();
    public int MinGuests { get; set; }
    public int MaxGuests { get; set; }
    public Guid HostId { get; set; }
    public bool AutoApproval { get; set; }
    public double? AverageRating { get; set; }
}

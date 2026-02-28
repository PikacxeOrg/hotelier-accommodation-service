namespace AccommodationService.Domain;

/// <summary>
/// Published when a new accommodation is created.
/// Consumed by search-service to index the accommodation.
/// </summary>
public record AccommodationCreated
{
    public Guid AccommodationId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public List<string> Amenities { get; init; } = new();
    public int MinGuests { get; init; }
    public int MaxGuests { get; init; }
    public Guid HostId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

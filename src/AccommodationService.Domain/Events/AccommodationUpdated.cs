namespace AccommodationService.Domain;

/// <summary>
/// Published when an accommodation is updated.
/// Consumed by search-service to update the index.
/// </summary>
public record AccommodationUpdated
{
    public Guid AccommodationId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public List<string> Amenities { get; init; } = new();
    public int MinGuests { get; init; }
    public int MaxGuests { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

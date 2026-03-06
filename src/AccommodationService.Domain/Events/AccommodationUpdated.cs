namespace Hotelier.Events;

/// <summary>
/// Published when an accommodation is updated.
/// Consumed by search-service to update the index.
/// </summary>
public record AccommodationUpdated
{
    public Guid AccommodationId { get; init; }
    public Guid HostId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public List<string> Amenities { get; init; } = new();
    public List<string> Pictures { get; init; } = new();
    public int MinGuests { get; init; }
    public int MaxGuests { get; init; }
    public bool AutoApproval { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

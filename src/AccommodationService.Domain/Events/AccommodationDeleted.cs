namespace Hotelier.Events;

/// <summary>
/// Published when an accommodation is deleted.
/// Consumed by search-service, availability-service, reservation-service.
/// </summary>
public record AccommodationDeleted
{
    public Guid AccommodationId { get; init; }
    public Guid HostId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

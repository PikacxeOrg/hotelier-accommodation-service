namespace AccommodationService.Domain;

/// <summary>
/// Published when accommodation images/assets are added or changed.
/// Consumed by cdn-service for processing (thumbnails, webp, etc.).
/// </summary>
public record AccommodationAssetUpdated
{
    public Guid AccommodationId { get; init; }
    public List<string> AssetUrls { get; init; } = new();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

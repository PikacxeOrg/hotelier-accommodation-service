namespace Hotelier.Events;

/// <summary>
/// Consumer-side DTO for CdnAssetProcessed.
/// </summary>
public record CdnAssetProcessed
{
    public string AssetId { get; init; } = string.Empty;
    public Guid OwnerId { get; init; }
    public Guid? EntityId { get; init; }
    public string Url { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
}

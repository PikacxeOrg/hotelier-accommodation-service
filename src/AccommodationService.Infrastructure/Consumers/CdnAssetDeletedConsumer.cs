using Hotelier.Events;

using MassTransit;

using Microsoft.Extensions.Logging;

namespace AccommodationService.Infrastructure;

/// <summary>
/// When a CDN asset is deleted, remove the URL from the accommodation's Pictures list.
/// </summary>
public class CdnAssetDeletedConsumer(
    AccommodationDbContext db,
    ILogger<CdnAssetDeletedConsumer> logger)
    : IConsumer<CdnAssetDeleted>
{
    public async Task Consume(ConsumeContext<CdnAssetDeleted> context)
    {
        var msg = context.Message;

        if (msg.EntityId is null)
        {
            logger.LogDebug("CdnAssetDeleted has no EntityId – skipping");
            return;
        }

        var accommodation = await db.Accommodations.FindAsync(msg.EntityId.Value);
        if (accommodation is null)
        {
            logger.LogWarning("Accommodation {EntityId} not found for deleted asset {AssetId}", msg.EntityId, msg.AssetId);
            return;
        }

        if (accommodation.Pictures.Remove(msg.Url))
        {
            accommodation.ModifiedBy = "system:cdn-asset-deleted";
            await db.SaveChangesAsync();

            logger.LogInformation("Removed picture {Url} from accommodation {Id}", msg.Url, msg.EntityId);
        }
    }
}

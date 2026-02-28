using Hotelier.Events;

using MassTransit;

using Microsoft.Extensions.Logging;

namespace AccommodationService.Infrastructure;

/// <summary>
/// When a CDN asset is processed, add the URL to the accommodation's Pictures list.
/// </summary>
public class CdnAssetProcessedConsumer(
    AccommodationDbContext db,
    ILogger<CdnAssetProcessedConsumer> logger)
    : IConsumer<CdnAssetProcessed>
{
    public async Task Consume(ConsumeContext<CdnAssetProcessed> context)
    {
        var msg = context.Message;

        if (msg.EntityId is null)
        {
            logger.LogDebug("CdnAssetProcessed has no EntityId – skipping");
            return;
        }

        var accommodation = await db.Accommodations.FindAsync(msg.EntityId.Value);
        if (accommodation is null)
        {
            logger.LogWarning("Accommodation {EntityId} not found for asset {AssetId}", msg.EntityId, msg.AssetId);
            return;
        }

        if (!accommodation.Pictures.Contains(msg.Url))
        {
            accommodation.Pictures.Add(msg.Url);
            accommodation.ModifiedBy = "system:cdn-asset-processed";
            await db.SaveChangesAsync();

            logger.LogInformation("Added picture {Url} to accommodation {Id}", msg.Url, msg.EntityId);
        }
    }
}

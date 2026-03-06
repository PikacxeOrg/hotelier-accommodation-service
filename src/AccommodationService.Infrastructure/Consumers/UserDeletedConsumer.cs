using Hotelier.Events;

using MassTransit;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccommodationService.Infrastructure;

/// <summary>
/// When a host deletes their account, cascade-delete all their accommodations
/// and publish AccommodationDeleted for each so downstream services clean up.
/// </summary>
public class UserDeletedConsumer(
    AccommodationDbContext db,
    IPublishEndpoint publisher,
    ILogger<UserDeletedConsumer> logger)
    : IConsumer<UserDeleted>
{
    public async Task Consume(ConsumeContext<UserDeleted> context)
    {
        var msg = context.Message;

        if (msg.UserType != "Host")
        {
            logger.LogDebug("UserDeleted for non-Host user {UserId} – skipping", msg.UserId);
            return;
        }

        logger.LogInformation("Host {UserId} deleted – removing all accommodations", msg.UserId);

        var accommodations = await db.Accommodations
            .Where(a => a.HostId == msg.UserId)
            .ToListAsync();

        foreach (var accommodation in accommodations)
        {
            db.Accommodations.Remove(accommodation);

            await publisher.Publish(new AccommodationDeleted
            {
                AccommodationId = accommodation.Id,
                HostId = accommodation.HostId
            });

            logger.LogInformation("Cascade-deleted accommodation {Id}", accommodation.Id);
        }

        if (accommodations.Count > 0)
            await db.SaveChangesAsync();
    }
}

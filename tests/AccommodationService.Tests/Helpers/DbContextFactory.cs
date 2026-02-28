using AccommodationService.Domain;
using AccommodationService.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace AccommodationService.Tests;

public static class DbContextFactory
{
    public static AccommodationDbContext Create(string? dbName = null)
    {
        dbName ??= Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AccommodationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var context = new AccommodationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public static Accommodation SeedAccommodation(
        AccommodationDbContext db,
        Guid? hostId = null,
        string name = "Test Hotel",
        string location = "Test City")
    {
        var accommodation = new Accommodation
        {
            Id = Guid.NewGuid(),
            Name = name,
            Location = location,
            Amenities = ["wifi", "parking"],
            Pictures = [],
            MinGuests = 1,
            MaxGuests = 4,
            HostId = hostId ?? Guid.NewGuid(),
            AutoApproval = false,
            CreatedBy = hostId?.ToString()
        };
        db.Accommodations.Add(accommodation);
        db.SaveChanges();
        return accommodation;
    }
}

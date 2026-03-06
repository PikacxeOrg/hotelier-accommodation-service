using AccommodationService.Domain;

using Microsoft.EntityFrameworkCore;

namespace AccommodationService.Infrastructure;

public class AccommodationDbContext : DbContext
{
    public AccommodationDbContext(DbContextOptions<AccommodationDbContext> options) : base(options) { }

    public DbSet<Accommodation> Accommodations => Set<Accommodation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Accommodation>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.HostId);
            entity.HasIndex(a => a.Location);

            // Store lists as JSON columns
            entity.Property(a => a.Amenities).HasColumnType("jsonb");
            entity.Property(a => a.Pictures).HasColumnType("jsonb");
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<TrackableEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.ModifiedTimestamp = DateTime.UtcNow;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}

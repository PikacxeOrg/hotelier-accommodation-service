using System.Security.Claims;

using AccommodationService.Domain;
using AccommodationService.Infrastructure;

using MassTransit;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccommodationService.Api;

[ApiController]
[Route("api/[controller]")]
public class AccommodationController(
    AccommodationDbContext db,
    IPublishEndpoint publisher,
    ILogger<AccommodationController> logger) : ControllerBase
{
    /// <summary>
    /// Create a new accommodation. Requires Host role.
    /// </summary>
    [Authorize(Roles = "Host")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccommodationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var hostId = GetUserId();
        if (hostId is null) return Unauthorized();

        if (request.MinGuests > request.MaxGuests)
            return BadRequest(new { message = "MinGuests cannot exceed MaxGuests." });

        var accommodation = new Accommodation
        {
            Name = request.Name,
            Location = request.Location,
            Amenities = request.Amenities,
            MinGuests = request.MinGuests,
            MaxGuests = request.MaxGuests,
            HostId = hostId.Value,
            AutoApproval = request.AutoApproval,
            CreatedBy = hostId.Value.ToString()
        };

        db.Accommodations.Add(accommodation);
        await db.SaveChangesAsync();

        await publisher.Publish(new AccommodationCreated
        {
            AccommodationId = accommodation.Id,
            Name = accommodation.Name,
            Location = accommodation.Location,
            Amenities = accommodation.Amenities,
            MinGuests = accommodation.MinGuests,
            MaxGuests = accommodation.MaxGuests,
            HostId = accommodation.HostId
        });

        logger.LogInformation("Accommodation {Id} created by Host {HostId}", accommodation.Id, hostId);

        return CreatedAtAction(nameof(GetById), new { id = accommodation.Id }, MapResponse(accommodation));
    }

    /// <summary>
    /// Get accommodation by ID.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var accommodation = await db.Accommodations.FindAsync(id);
        if (accommodation is null) return NotFound();
        return Ok(MapResponse(accommodation));
    }

    /// <summary>
    /// List all accommodations, with optional search filters.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? location,
        [FromQuery] int? guests,
        [FromQuery] string? amenity)
    {
        var query = db.Accommodations.AsQueryable();

        if (!string.IsNullOrWhiteSpace(location))
            query = query.Where(a => a.Location.ToLower().Contains(location.ToLower()));

        if (guests.HasValue)
            query = query.Where(a => a.MinGuests <= guests.Value && a.MaxGuests >= guests.Value);

        var results = await query.OrderBy(a => a.Name).ToListAsync();

        // Filter amenities in memory since jsonb contains queries vary by provider
        if (!string.IsNullOrWhiteSpace(amenity))
            results = results.Where(a =>
                a.Amenities.Any(am => am.Contains(amenity, StringComparison.OrdinalIgnoreCase))).ToList();

        return Ok(results.Select(MapResponse));
    }

    /// <summary>
    /// List accommodations by host ID.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("host/{hostId:guid}")]
    public async Task<IActionResult> ListByHost(Guid hostId)
    {
        var accommodations = await db.Accommodations
            .Where(a => a.HostId == hostId)
            .OrderBy(a => a.Name)
            .ToListAsync();

        return Ok(accommodations.Select(MapResponse));
    }

    /// <summary>
    /// List the current host's accommodations.
    /// </summary>
    [Authorize(Roles = "Host")]
    [HttpGet("mine")]
    public async Task<IActionResult> ListMine()
    {
        var hostId = GetUserId();
        if (hostId is null) return Unauthorized();

        var accommodations = await db.Accommodations
            .Where(a => a.HostId == hostId.Value)
            .OrderBy(a => a.Name)
            .ToListAsync();

        return Ok(accommodations.Select(MapResponse));
    }

    /// <summary>
    /// Update an accommodation. Only the owning host may update.
    /// </summary>
    [Authorize(Roles = "Host")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccommodationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var hostId = GetUserId();
        if (hostId is null) return Unauthorized();

        var accommodation = await db.Accommodations.FindAsync(id);
        if (accommodation is null) return NotFound();

        if (accommodation.HostId != hostId.Value)
            return Forbid();

        if (request.Name is not null) accommodation.Name = request.Name;
        if (request.Location is not null) accommodation.Location = request.Location;
        if (request.Amenities is not null) accommodation.Amenities = request.Amenities;
        if (request.MinGuests.HasValue) accommodation.MinGuests = request.MinGuests.Value;
        if (request.MaxGuests.HasValue) accommodation.MaxGuests = request.MaxGuests.Value;
        if (request.AutoApproval.HasValue) accommodation.AutoApproval = request.AutoApproval.Value;

        var min = request.MinGuests ?? accommodation.MinGuests;
        var max = request.MaxGuests ?? accommodation.MaxGuests;
        if (min > max)
            return BadRequest(new { message = "MinGuests cannot exceed MaxGuests." });

        accommodation.ModifiedBy = hostId.Value.ToString();
        await db.SaveChangesAsync();

        await publisher.Publish(new AccommodationUpdated
        {
            AccommodationId = accommodation.Id,
            Name = accommodation.Name,
            Location = accommodation.Location,
            Amenities = accommodation.Amenities,
            MinGuests = accommodation.MinGuests,
            MaxGuests = accommodation.MaxGuests
        });

        logger.LogInformation("Accommodation {Id} updated by Host {HostId}", id, hostId);

        return Ok(MapResponse(accommodation));
    }

    /// <summary>
    /// Delete an accommodation. Only the owning host may delete.
    /// </summary>
    [Authorize(Roles = "Host")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var hostId = GetUserId();
        if (hostId is null) return Unauthorized();

        var accommodation = await db.Accommodations.FindAsync(id);
        if (accommodation is null) return NotFound();

        if (accommodation.HostId != hostId.Value)
            return Forbid();

        db.Accommodations.Remove(accommodation);
        await db.SaveChangesAsync();

        await publisher.Publish(new AccommodationDeleted
        {
            AccommodationId = accommodation.Id,
            HostId = accommodation.HostId
        });

        logger.LogInformation("Accommodation {Id} deleted by Host {HostId}", id, hostId);

        return NoContent();
    }

    // -------------------------------------------------------
    // Helpers 
    // -------------------------------------------------------
    private Guid? GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static AccommodationResponse MapResponse(Accommodation a) => new()
    {
        Id = a.Id,
        Name = a.Name,
        Location = a.Location,
        Amenities = a.Amenities,
        Pictures = a.Pictures,
        MinGuests = a.MinGuests,
        MaxGuests = a.MaxGuests,
        HostId = a.HostId,
        AutoApproval = a.AutoApproval
    };
}

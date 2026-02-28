using System.ComponentModel.DataAnnotations;

namespace AccommodationService.Domain;

public class Accommodation : TrackableEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// List of amenities (wifi, kitchen, AC, parking, etc.)
    /// Stored as a JSON array column.
    /// </summary>
    public List<string> Amenities { get; set; } = new();

    /// <summary>
    /// URLs/paths to accommodation photos (managed via cdn-service).
    /// Stored as a JSON array column.
    /// </summary>
    public List<string> Pictures { get; set; } = new();

    [Required]
    [Range(1, 100)]
    public int MinGuests { get; set; }

    [Required]
    [Range(1, 100)]
    public int MaxGuests { get; set; }

    /// <summary>
    /// The host (user) who owns this accommodation.
    /// </summary>
    [Required]
    public Guid HostId { get; set; }

    /// <summary>
    /// Whether reservations are automatically approved or require manual host approval.
    /// </summary>
    public bool AutoApproval { get; set; }
}

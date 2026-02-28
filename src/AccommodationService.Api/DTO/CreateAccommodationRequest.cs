using System.ComponentModel.DataAnnotations;

namespace AccommodationService.Api;

public class CreateAccommodationRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Location { get; set; } = string.Empty;

    public List<string> Amenities { get; set; } = new();

    [Required]
    [Range(1, 100)]
    public int MinGuests { get; set; }

    [Required]
    [Range(1, 100)]
    public int MaxGuests { get; set; }

    public bool AutoApproval { get; set; }
}

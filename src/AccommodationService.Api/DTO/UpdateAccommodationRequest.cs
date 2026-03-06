using System.ComponentModel.DataAnnotations;

namespace AccommodationService.Api;

public class UpdateAccommodationRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(500)]
    public string? Location { get; set; }

    public List<string>? Amenities { get; set; }
    public List<string>? Pictures { get; set; }

    [Range(1, 100)]
    public int? MinGuests { get; set; }

    [Range(1, 100)]
    public int? MaxGuests { get; set; }

    public bool? AutoApproval { get; set; }
}

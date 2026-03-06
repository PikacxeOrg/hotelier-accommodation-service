using System.Security.Claims;

using AccommodationService.Api;
using AccommodationService.Infrastructure;

using Hotelier.Events;

using FluentAssertions;

using MassTransit;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Moq;

namespace AccommodationService.Tests;

public class AccommodationControllerTests : IDisposable
{
    private readonly AccommodationDbContext _db;
    private readonly Mock<IPublishEndpoint> _publisherMock;
    private readonly AccommodationController _sut;
    private readonly Guid _hostId = Guid.NewGuid();

    public AccommodationControllerTests()
    {
        _db = DbContextFactory.Create();
        _publisherMock = new Mock<IPublishEndpoint>();
        var logger = new Mock<ILogger<AccommodationController>>();

        _sut = new AccommodationController(_db, _publisherMock.Object, logger.Object);
        SetAuthenticatedUser(_hostId);
    }

    public void Dispose() => _db.Dispose();

    // -- Create --------------------------------------------------

    [Fact]
    public async Task Create_WithValidData_ReturnsCreated()
    {
        var request = MakeCreateRequest();

        var result = await _sut.Create(request);

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task Create_SavesAccommodationInDatabase()
    {
        var request = MakeCreateRequest();

        await _sut.Create(request);

        _db.Accommodations.Should().HaveCount(1);
        var saved = _db.Accommodations.First();
        saved.Name.Should().Be("My Hotel");
        saved.HostId.Should().Be(_hostId);
    }

    [Fact]
    public async Task Create_PublishesAccommodationCreatedEvent()
    {
        var request = MakeCreateRequest();

        await _sut.Create(request);

        _publisherMock.Verify(p =>
            p.Publish(It.Is<AccommodationCreated>(e =>
                e.Name == "My Hotel" && e.HostId == _hostId),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_MinGuestsExceedsMaxGuests_ReturnsBadRequest()
    {
        var request = MakeCreateRequest();
        request.MinGuests = 10;
        request.MaxGuests = 2;

        var result = await _sut.Create(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_Unauthenticated_ReturnsUnauthorized()
    {
        SetUnauthenticated();
        var request = MakeCreateRequest();

        var result = await _sut.Create(request);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Create_SetsCreatedBy()
    {
        var request = MakeCreateRequest();

        await _sut.Create(request);

        _db.Accommodations.First().CreatedBy.Should().Be(_hostId.ToString());
    }

    // -- GetById -------------------------------------------------

    [Fact]
    public async Task GetById_ExistingAccommodation_ReturnsOk()
    {
        var accommodation = DbContextFactory.SeedAccommodation(_db, _hostId);

        var result = await _sut.GetById(accommodation.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<AccommodationResponse>().Subject;
        response.Id.Should().Be(accommodation.Id);
        response.Name.Should().Be(accommodation.Name);
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNotFound()
    {
        var result = await _sut.GetById(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    // -- List ----------------------------------------------------

    [Fact]
    public async Task List_ReturnsAllAccommodations()
    {
        DbContextFactory.SeedAccommodation(_db, _hostId, "Hotel A", "City A");
        DbContextFactory.SeedAccommodation(_db, _hostId, "Hotel B", "City B");

        var result = await _sut.List(null, null, null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IEnumerable<AccommodationResponse>>().Subject;
        list.Should().HaveCount(2);
    }

    [Fact]
    public async Task List_FiltersByLocation()
    {
        DbContextFactory.SeedAccommodation(_db, _hostId, "Hotel A", "Paris");
        DbContextFactory.SeedAccommodation(_db, _hostId, "Hotel B", "London");

        var result = await _sut.List("paris", null, null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IEnumerable<AccommodationResponse>>().Subject;
        list.Should().HaveCount(1);
        list.First().Location.Should().Be("Paris");
    }

    [Fact]
    public async Task List_FiltersByGuestCount()
    {
        DbContextFactory.SeedAccommodation(_db, _hostId, "Small", "City"); // min=1, max=4
        var big = DbContextFactory.SeedAccommodation(_db, _hostId, "Big", "City");
        big.MinGuests = 5;
        big.MaxGuests = 10;
        _db.SaveChanges();

        var result = await _sut.List(null, 3, null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IEnumerable<AccommodationResponse>>().Subject;
        list.Should().HaveCount(1);
        list.First().Name.Should().Be("Small");
    }

    [Fact]
    public async Task List_FiltersByAmenity()
    {
        var a1 = DbContextFactory.SeedAccommodation(_db, _hostId, "With Kitchen", "City");
        a1.Amenities = ["wifi", "kitchen"];
        var a2 = DbContextFactory.SeedAccommodation(_db, _hostId, "No Kitchen", "City");
        a2.Amenities = ["wifi"];
        _db.SaveChanges();

        var result = await _sut.List(null, null, "kitchen");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IEnumerable<AccommodationResponse>>().Subject;
        list.Should().HaveCount(1);
        list.First().Name.Should().Be("With Kitchen");
    }

    [Fact]
    public async Task List_NoResults_ReturnsEmptyList()
    {
        var result = await _sut.List(null, null, null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IEnumerable<AccommodationResponse>>().Subject;
        list.Should().BeEmpty();
    }

    // -- ListByHost ----------------------------------------------

    [Fact]
    public async Task ListByHost_ReturnsHostAccommodations()
    {
        DbContextFactory.SeedAccommodation(_db, _hostId, "Mine", "City");
        DbContextFactory.SeedAccommodation(_db, Guid.NewGuid(), "Other", "City");

        var result = await _sut.ListByHost(_hostId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IEnumerable<AccommodationResponse>>().Subject;
        list.Should().HaveCount(1);
        list.First().Name.Should().Be("Mine");
    }

    // -- ListMine ------------------------------------------------

    [Fact]
    public async Task ListMine_ReturnsCurrentHostAccommodations()
    {
        DbContextFactory.SeedAccommodation(_db, _hostId, "Mine", "City");
        DbContextFactory.SeedAccommodation(_db, Guid.NewGuid(), "Other", "City");

        var result = await _sut.ListMine();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IEnumerable<AccommodationResponse>>().Subject;
        list.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListMine_Unauthenticated_ReturnsUnauthorized()
    {
        SetUnauthenticated();

        var result = await _sut.ListMine();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // -- Update --------------------------------------------------

    [Fact]
    public async Task Update_ValidRequest_ReturnsOkWithUpdatedData()
    {
        var accommodation = DbContextFactory.SeedAccommodation(_db, _hostId);

        var result = await _sut.Update(accommodation.Id, new UpdateAccommodationRequest
        {
            Name = "Updated Name"
        });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<AccommodationResponse>().Subject;
        response.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task Update_UpdatesAllFields()
    {
        var accommodation = DbContextFactory.SeedAccommodation(_db, _hostId);

        await _sut.Update(accommodation.Id, new UpdateAccommodationRequest
        {
            Name = "New Name",
            Location = "New Location",
            Amenities = ["pool", "spa"],
            MinGuests = 2,
            MaxGuests = 8,
            AutoApproval = true
        });

        var saved = _db.Accommodations.Find(accommodation.Id)!;
        saved.Name.Should().Be("New Name");
        saved.Location.Should().Be("New Location");
        saved.Amenities.Should().BeEquivalentTo(["pool", "spa"]);
        saved.MinGuests.Should().Be(2);
        saved.MaxGuests.Should().Be(8);
        saved.AutoApproval.Should().BeTrue();
    }

    [Fact]
    public async Task Update_PublishesAccommodationUpdatedEvent()
    {
        var accommodation = DbContextFactory.SeedAccommodation(_db, _hostId);

        await _sut.Update(accommodation.Id, new UpdateAccommodationRequest { Name = "X" });

        _publisherMock.Verify(p =>
            p.Publish(It.Is<AccommodationUpdated>(e =>
                e.AccommodationId == accommodation.Id && e.Name == "X"),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_NonExistent_ReturnsNotFound()
    {
        var result = await _sut.Update(Guid.NewGuid(), new UpdateAccommodationRequest { Name = "X" });

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Update_OtherHostsAccommodation_ReturnsForbid()
    {
        var otherHostId = Guid.NewGuid();
        var accommodation = DbContextFactory.SeedAccommodation(_db, otherHostId);

        var result = await _sut.Update(accommodation.Id, new UpdateAccommodationRequest { Name = "X" });

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Update_MinGuestsExceedsMaxGuests_ReturnsBadRequest()
    {
        var accommodation = DbContextFactory.SeedAccommodation(_db, _hostId);

        var result = await _sut.Update(accommodation.Id, new UpdateAccommodationRequest
        {
            MinGuests = 10,
            MaxGuests = 2
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Update_Unauthenticated_ReturnsUnauthorized()
    {
        SetUnauthenticated();
        var accommodation = DbContextFactory.SeedAccommodation(_db, _hostId);

        var result = await _sut.Update(accommodation.Id, new UpdateAccommodationRequest { Name = "X" });

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Update_SetsModifiedBy()
    {
        var accommodation = DbContextFactory.SeedAccommodation(_db, _hostId);

        await _sut.Update(accommodation.Id, new UpdateAccommodationRequest { Name = "X" });

        _db.Accommodations.Find(accommodation.Id)!.ModifiedBy.Should().Be(_hostId.ToString());
    }

    // -- Update pictures -----------------------------------------

    [Fact]
    public async Task Update_WithNewPictures_AddsPicturesToExistingList()
    {
        var accommodation = DbContextFactory.SeedAccommodation(_db, _hostId);
        accommodation.Pictures = ["http://cdn/existing.jpg"];
        _db.SaveChanges();

        await _sut.Update(accommodation.Id, new UpdateAccommodationRequest
        {
            Pictures = ["http://cdn/new1.jpg", "http://cdn/new2.jpg"]
        });

        var saved = _db.Accommodations.Find(accommodation.Id)!;
        saved.Pictures.Should().HaveCount(3);
        saved.Pictures.Should().Contain("http://cdn/existing.jpg");
        saved.Pictures.Should().Contain("http://cdn/new1.jpg");
        saved.Pictures.Should().Contain("http://cdn/new2.jpg");
    }

    [Fact]
    public async Task Update_WithPictures_DeduplicatesExistingUrls()
    {
        var accommodation = DbContextFactory.SeedAccommodation(_db, _hostId);
        accommodation.Pictures = ["http://cdn/existing.jpg"];
        _db.SaveChanges();

        await _sut.Update(accommodation.Id, new UpdateAccommodationRequest
        {
            Pictures = ["http://cdn/existing.jpg", "http://cdn/new.jpg"]
        });

        var saved = _db.Accommodations.Find(accommodation.Id)!;
        saved.Pictures.Should().HaveCount(2);
        saved.Pictures.Should().ContainSingle(p => p == "http://cdn/existing.jpg");
    }

    [Fact]
    public async Task Update_WithPictures_WhenListWasEmpty_SetsListDirectly()
    {
        var accommodation = DbContextFactory.SeedAccommodation(_db, _hostId);
        // SeedAccommodation sets Pictures = [] already

        await _sut.Update(accommodation.Id, new UpdateAccommodationRequest
        {
            Pictures = ["http://cdn/first.jpg"]
        });

        var saved = _db.Accommodations.Find(accommodation.Id)!;
        saved.Pictures.Should().ContainSingle().Which.Should().Be("http://cdn/first.jpg");
    }

    [Fact]
    public async Task Update_WithNullPictures_DoesNotChangePictures()
    {
        var accommodation = DbContextFactory.SeedAccommodation(_db, _hostId);
        accommodation.Pictures = ["http://cdn/original.jpg"];
        _db.SaveChanges();

        await _sut.Update(accommodation.Id, new UpdateAccommodationRequest
        {
            Name = "Updated Name",
            Pictures = null
        });

        var saved = _db.Accommodations.Find(accommodation.Id)!;
        saved.Pictures.Should().ContainSingle().Which.Should().Be("http://cdn/original.jpg");
    }

    [Fact]
    public async Task Update_Pictures_IncludedInResponse()
    {
        var accommodation = DbContextFactory.SeedAccommodation(_db, _hostId);

        var result = await _sut.Update(accommodation.Id, new UpdateAccommodationRequest
        {
            Pictures = ["http://cdn/photo.jpg"]
        });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<AccommodationResponse>().Subject;
        response.Pictures.Should().ContainSingle().Which.Should().Be("http://cdn/photo.jpg");
    }

    // -- Delete --------------------------------------------------

    [Fact]
    public async Task Delete_OwnAccommodation_ReturnsNoContent()
    {
        var accommodation = DbContextFactory.SeedAccommodation(_db, _hostId);

        var result = await _sut.Delete(accommodation.Id);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_RemovesFromDatabase()
    {
        var accommodation = DbContextFactory.SeedAccommodation(_db, _hostId);

        await _sut.Delete(accommodation.Id);

        _db.Accommodations.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_PublishesAccommodationDeletedEvent()
    {
        var accommodation = DbContextFactory.SeedAccommodation(_db, _hostId);

        await _sut.Delete(accommodation.Id);

        _publisherMock.Verify(p =>
            p.Publish(It.Is<AccommodationDeleted>(e =>
                e.AccommodationId == accommodation.Id && e.HostId == _hostId),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_NonExistent_ReturnsNotFound()
    {
        var result = await _sut.Delete(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Delete_OtherHostsAccommodation_ReturnsForbid()
    {
        var accommodation = DbContextFactory.SeedAccommodation(_db, Guid.NewGuid());

        var result = await _sut.Delete(accommodation.Id);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Delete_Unauthenticated_ReturnsUnauthorized()
    {
        SetUnauthenticated();
        var accommodation = DbContextFactory.SeedAccommodation(_db, _hostId);

        var result = await _sut.Delete(accommodation.Id);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // -- helpers -------------------------------------------------

    private void SetAuthenticatedUser(Guid userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "Test");
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private void SetUnauthenticated()
    {
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };
    }

    private static CreateAccommodationRequest MakeCreateRequest() => new()
    {
        Name = "My Hotel",
        Location = "Test City",
        Amenities = ["wifi", "parking", "kitchen"],
        MinGuests = 1,
        MaxGuests = 6,
        AutoApproval = false
    };
}

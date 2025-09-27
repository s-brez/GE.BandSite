using NodaTime;

namespace GE.BandSite.Server.Features.Organization.Models;

public sealed record EventListingModel(string Title, LocalDate? EventDate, string? Location, string? Description);

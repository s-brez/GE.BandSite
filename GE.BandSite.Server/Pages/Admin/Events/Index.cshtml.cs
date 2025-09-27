using GE.BandSite.Database.Organization;
using GE.BandSite.Server.Features.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GE.BandSite.Server.Pages.Admin.Events;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IOrganizationAdminService _adminService;

    public IndexModel(IOrganizationAdminService adminService)
    {
        _adminService = adminService;
    }

    public IReadOnlyList<EventListing> Events { get; private set; } = Array.Empty<EventListing>();

    [BindProperty]
    public EventInputModel Input { get; set; } = new();

    public async Task OnGetAsync()
    {
        Events = await _adminService.GetEventsAsync().ConfigureAwait(false);
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Events = await _adminService.GetEventsAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }

        var entity = Input.ToEntity();
        await _adminService.AddOrUpdateEventAsync(entity, cancellationToken).ConfigureAwait(false);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetEditAsync(Guid id, CancellationToken cancellationToken)
    {
        Events = await _adminService.GetEventsAsync(cancellationToken).ConfigureAwait(false);
        var listing = Events.FirstOrDefault(x => x.Id == id);
        if (listing != null)
        {
            Input = new EventInputModel
            {
                Id = listing.Id,
                Title = listing.Title,
                Location = listing.Location,
                Description = listing.Description,
                EventDate = listing.EventDate?.ToDateTimeUnspecified(),
                DisplayOrder = listing.DisplayOrder,
                IsPublished = listing.IsPublished
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _adminService.DeleteEventAsync(id, cancellationToken).ConfigureAwait(false);
        return RedirectToPage();
    }
}

public sealed class EventInputModel
{
    public Guid Id { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    public string Title { get; set; } = string.Empty;

    public DateTime? EventDate { get; set; }

    public string? Location { get; set; }

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsPublished { get; set; }

    public EventListing ToEntity()
    {
        var entity = new EventListing
        {
            Id = Id,
            Title = Title,
            Location = Location,
            Description = Description,
            DisplayOrder = DisplayOrder,
            IsPublished = IsPublished
        };

        if (EventDate.HasValue)
        {
            entity.EventDate = NodaTime.LocalDate.FromDateTime(EventDate.Value);
        }

        return entity;
    }
}

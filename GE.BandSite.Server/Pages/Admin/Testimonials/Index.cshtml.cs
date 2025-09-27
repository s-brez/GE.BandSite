using GE.BandSite.Database.Organization;
using GE.BandSite.Server.Features.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GE.BandSite.Server.Pages.Admin.Testimonials;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IOrganizationAdminService _adminService;

    public IndexModel(IOrganizationAdminService adminService)
    {
        _adminService = adminService;
    }

    public IReadOnlyList<Testimonial> Testimonials { get; private set; } = Array.Empty<Testimonial>();

    [BindProperty]
    public Testimonial Input { get; set; } = new();

    public async Task OnGetAsync()
    {
        Testimonials = await _adminService.GetTestimonialsAsync().ConfigureAwait(false);
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Testimonials = await _adminService.GetTestimonialsAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }

        await _adminService.AddOrUpdateTestimonialAsync(Input, cancellationToken).ConfigureAwait(false);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _adminService.DeleteTestimonialAsync(id, cancellationToken).ConfigureAwait(false);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetEditAsync(Guid id, CancellationToken cancellationToken)
    {
        Testimonials = await _adminService.GetTestimonialsAsync(cancellationToken).ConfigureAwait(false);
        var testimonial = Testimonials.FirstOrDefault(x => x.Id == id);
        if (testimonial != null)
        {
            Input = new Testimonial
            {
                Id = testimonial.Id,
                Quote = testimonial.Quote,
                Name = testimonial.Name,
                Role = testimonial.Role,
                DisplayOrder = testimonial.DisplayOrder,
                IsPublished = testimonial.IsPublished,
                CreatedAt = testimonial.CreatedAt
            };
        }

        return Page();
    }
}

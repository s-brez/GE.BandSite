using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GE.BandSite.Server.Pages.Admin;

[Authorize]
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}

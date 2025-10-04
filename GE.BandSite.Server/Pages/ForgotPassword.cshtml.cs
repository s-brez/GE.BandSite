using System.ComponentModel.DataAnnotations;
using GE.BandSite.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GE.BandSite.Server.Pages;

public sealed class ForgotPasswordModel : PageModel
{
    private readonly IPasswordResetService _passwordResetService;
    private const string ResetRequestedKey = "PasswordResetRequested";

    public ForgotPasswordModel(IPasswordResetService passwordResetService)
    {
        _passwordResetService = passwordResetService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool RequestSent { get; private set; }

    public void OnGet()
    {
        if (TempData.TryGetValue(ResetRequestedKey, out var resetRequested) && resetRequested is string flag && bool.TryParse(flag, out var parsedFlag))
        {
            RequestSent = parsedFlag;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Page();
        }

        await _passwordResetService.RequestPasswordResetAsync(Input.Email, HttpContext.RequestAborted).ConfigureAwait(false);

        TempData[ResetRequestedKey] = bool.TrueString;

        return RedirectToPage();
    }

    public sealed class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}

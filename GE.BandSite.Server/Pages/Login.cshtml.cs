using System.ComponentModel.DataAnnotations;
using GE.BandSite.Server.Authentication;
using GE.BandSite.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Primitives;

namespace GE.BandSite.Server.Pages;

public class LoginModel : PageModel
{
    private readonly ILoginService _loginService;
    public LoginModel(ILoginService loginService)
    {
        _loginService = loginService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? RedirectPath { get; private set; }

    public void OnGet()
    {
        RedirectPath = GetRedirectTarget();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        RedirectPath = GetRedirectTarget();

        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Page();
        }

        var result = await _loginService.AuthenticateAsync(Input.Email, Input.Password, HttpContext, HttpContext.RequestAborted).ConfigureAwait(false);
        if (!result.Success)
        {
            var errorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? "Unable to sign in. Please try again."
                : result.ErrorMessage;

            ModelState.AddModelError(string.Empty, errorMessage);

            if (result.ErrorStatus.HasValue)
            {
                Response.StatusCode = result.ErrorStatus.Value;
            }

            return Page();
        }

        if (IsLocalRedirect(RedirectPath))
        {
            return Redirect(RedirectPath!);
        }

        return RedirectToPage("/Admin/Index");
    }

    private string? GetRedirectTarget()
    {
        StringValues redirect = Request.Query[AuthenticationConfiguration.RedirectQueryKey];
        if (StringValues.IsNullOrEmpty(redirect))
        {
            if (Request.HasFormContentType)
            {
                redirect = Request.Form[AuthenticationConfiguration.RedirectQueryKey];
            }
        }

        var value = redirect.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool IsLocalRedirect(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.StartsWith("~/", StringComparison.Ordinal))
        {
            return true;
        }

        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        if (path.Length == 1)
        {
            return true;
        }

        var second = path[1];
        return second != '/' && second != '\\';
    }

    public sealed class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}

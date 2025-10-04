using System.ComponentModel.DataAnnotations;
using GE.BandSite.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GE.BandSite.Server.Pages;

public sealed class ResetPasswordModel : PageModel
{
    private readonly IPasswordResetService _passwordResetService;

    public ResetPasswordModel(IPasswordResetService passwordResetService)
    {
        _passwordResetService = passwordResetService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool Completed { get; private set; }

    public bool InvalidLink { get; private set; }

    public string? ErrorMessage { get; private set; }

    public void OnGet(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            InvalidLink = true;
            ErrorMessage = "The password reset link is invalid or missing.";
            Input = new InputModel();
            return;
        }

        Input.Token = token;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Input.Token))
        {
            InvalidLink = true;
            ErrorMessage = "This password reset link is invalid. Request a new link and try again.";
            Response.StatusCode = StatusCodes.Status400BadRequest;
            Input = new InputModel();
            return Page();
        }

        if (!string.Equals(Input.Password, Input.ConfirmPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(Input.ConfirmPassword), "Passwords must match.");
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Page();
        }

        var result = await _passwordResetService.ResetPasswordAsync(Input.Token, Input.Password, HttpContext.RequestAborted).ConfigureAwait(false);
        if (result.Success)
        {
            Completed = true;
            ModelState.Clear();
            Input = new InputModel();
            return Page();
        }

        Response.StatusCode = StatusCodes.Status400BadRequest;

        switch (result.Error)
        {
            case PasswordResetError.InvalidToken:
                InvalidLink = true;
                ErrorMessage = "This password reset link is invalid. Request a new link and try again.";
                Input = new InputModel();
                break;
            case PasswordResetError.Expired:
                InvalidLink = true;
                ErrorMessage = "This password reset link has expired. Request a new link and try again.";
                Input = new InputModel();
                break;
            case PasswordResetError.AlreadyUsed:
                InvalidLink = true;
                ErrorMessage = "This password reset link has already been used.";
                Input = new InputModel();
                break;
            case PasswordResetError.PasswordMatchesCurrent:
                ModelState.AddModelError(nameof(Input.Password), "Please choose a password you have not used before.");
                break;
            case PasswordResetError.PasswordValidationFailed:
                foreach (var validationError in result.PasswordValidationErrors)
                {
                    ModelState.AddModelError(nameof(Input.Password), validationError);
                }

                break;
            default:
                ModelState.AddModelError(string.Empty, "Unable to reset password. Request a new link and try again.");
                break;
        }

        return Page();
    }

    public sealed class InputModel
    {
        [HiddenInput]
        public string Token { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}

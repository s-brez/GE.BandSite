using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GE.BandSite.Server.Authentication;
using GE.BandSite.Server.Configuration;
using GE.BandSite.Server.Services;
using GE.BandSite.Server.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Options;
using NodaTime;
using Microsoft.IdentityModel.Tokens;
using ServerClaimTypes = GE.BandSite.Server.Authentication.ClaimTypes;

namespace GE.BandSite.Server.Pages;

public class LoginModel : PageModel
{
    private readonly ILoginService _loginService;
    private readonly IOptions<SystemUserOptions> _systemUserOptions;
    private readonly ISecurityTokenGenerator _securityTokenGenerator;
    private readonly IClock _clock;

    public LoginModel(
        ILoginService loginService,
        IOptions<SystemUserOptions> systemUserOptions,
        ISecurityTokenGenerator securityTokenGenerator,
        IClock clock)
    {
        _loginService = loginService;
        _systemUserOptions = systemUserOptions;
        _securityTokenGenerator = securityTokenGenerator;
        _clock = clock;
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

        var systemUserResult = TryAuthenticateSystemUser();

        var result = systemUserResult ?? await _loginService.AuthenticateAsync(Input.Email, Input.Password, HttpContext, HttpContext.RequestAborted).ConfigureAwait(false);
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

    private LoginServiceResult? TryAuthenticateSystemUser()
    {
        var options = _systemUserOptions.Value;
        if (!options.Enabled || !options.TryGetUser(Input.Email, out var credential))
        {
            return null;
        }

        if (!string.Equals(Input.Password, credential.Password, StringComparison.Ordinal))
        {
            return new LoginServiceResult
            {
                Success = false,
                ErrorStatus = StatusCodes.Status401Unauthorized,
                ErrorMessage = "Invalid email or password."
            };
        }

        IssueSystemUserTokens(Input.Email, credential);

        return new LoginServiceResult { Success = true };
    }

    private void IssueSystemUserTokens(string submittedUserName, SystemUserCredential credential)
    {
        var sessionTimeout = _systemUserOptions.Value.SessionTimeout;
        if (sessionTimeout <= TimeSpan.Zero)
        {
            sessionTimeout = TimeSpan.FromMinutes(AuthenticationConfiguration.AccessTokenExpirationMinutes);
        }

        var now = _clock.GetCurrentInstant();
        var expirationInstant = now.Plus(Duration.FromTimeSpan(sessionTimeout));

        var claims = new List<Claim>
        {
            new Claim(ServerClaimTypes.Email, submittedUserName),
            new Claim(ServerClaimTypes.FirstName, credential.UserName),
            new Claim(ServerClaimTypes.LastName, "System"),
            new Claim(ServerClaimTypes.UserId, $"system:{credential.UserName}".ToLowerInvariant())
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = HttpContext.Request.Host.Host ?? string.Empty,
            Audience = HttpContext.Request.Host.Host ?? string.Empty,
            IssuedAt = now.ToDateTimeUtc(),
            NotBefore = now.ToDateTimeUtc(),
            Expires = expirationInstant.ToDateTimeUtc(),
            Subject = new ClaimsIdentity(claims, "Custom")
        };

        var token = (JwtSecurityToken)_securityTokenGenerator.Generate(descriptor);
        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        HttpContext.Response.Cookies.Delete(AuthenticationConfiguration.RefreshTokenKey);
        HttpContext.Response.Cookies.Delete(AuthenticationConfiguration.AccessTokenKey);
        HttpContext.Response.Cookies.Append(AuthenticationConfiguration.AccessTokenKey, tokenString,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                MaxAge = sessionTimeout
            });
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
        [EmailOrSystemUserName]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}

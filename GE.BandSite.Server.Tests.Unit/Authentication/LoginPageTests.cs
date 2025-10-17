using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GE.BandSite.Server.Configuration;
using GE.BandSite.Server.Authentication;
using GE.BandSite.Server.Pages;
using GE.BandSite.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NodaTime;
using NUnit.Framework;

namespace GE.BandSite.Server.Tests.Authentication;

[TestFixture]
public class LoginPageTests
{
    [Test]
    public void OnGet_WithRedirectQuery_SetsRedirectPath()
    {
        var loginService = new RecordingLoginService();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString($"?{AuthenticationConfiguration.RedirectQueryKey}=/Admin/Events");

        var model = CreateModel(loginService, httpContext);

        model.OnGet();

        Assert.That(model.RedirectPath, Is.EqualTo("/Admin/Events"));
    }

    [Test]
    public async Task OnPostAsync_InvalidModel_ReturnsPageAndSkipsService()
    {
        var loginService = new RecordingLoginService();
        var model = CreateModel(loginService);
        model.ModelState.AddModelError("Input.Email", "Required");

        var result = await model.OnPostAsync();

        Assert.That(result, Is.TypeOf<PageResult>());
        Assert.That(loginService.CallCount, Is.EqualTo(0));
        Assert.That(model.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task OnPostAsync_WithValidCredentialsAndRedirect_ReturnsRedirect()
    {
        var loginService = new RecordingLoginService();
        loginService.Result = new LoginServiceResult { Success = true };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString($"?{AuthenticationConfiguration.RedirectQueryKey}=/Admin/Media");

        var model = CreateModel(loginService, httpContext);
        model.Input.Email = "admin@example.com";
        model.Input.Password = "StrongPassword1!";

        var result = await model.OnPostAsync();

        var redirect = result as RedirectResult;
        Assert.That(redirect, Is.Not.Null);
        Assert.That(redirect!.Url, Is.EqualTo("/Admin/Media"));
        Assert.That(loginService.CallCount, Is.EqualTo(1));
        Assert.That(loginService.LastEmail, Is.EqualTo("admin@example.com"));
    }

    [Test]
    public async Task OnPostAsync_WithValidCredentialsAndNoRedirect_GoesToAdminDashboard()
    {
        var loginService = new RecordingLoginService();
        loginService.Result = new LoginServiceResult { Success = true };

        var model = CreateModel(loginService);
        model.Input.Email = "admin@example.com";
        model.Input.Password = "StrongPassword1!";

        var result = await model.OnPostAsync();

        var redirect = result as RedirectToPageResult;
        Assert.That(redirect, Is.Not.Null);
        Assert.That(redirect!.PageName, Is.EqualTo("/Admin/Index"));
        Assert.That(loginService.CallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task OnPostAsync_WithInvalidCredentials_ReturnsPageWithError()
    {
        var loginService = new RecordingLoginService();
        loginService.Result = new LoginServiceResult
        {
            Success = false,
            ErrorStatus = StatusCodes.Status401Unauthorized,
            ErrorMessage = "Invalid email or password."
        };

        var model = CreateModel(loginService);
        model.Input.Email = "admin@example.com";
        model.Input.Password = "WrongPassword";

        var result = await model.OnPostAsync();

        Assert.That(result, Is.TypeOf<PageResult>());
        Assert.That(model.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
        Assert.That(model.ModelState[string.Empty]?.Errors, Is.Not.Null);
        Assert.That(model.ModelState[string.Empty]!.Errors[0].ErrorMessage, Is.EqualTo("Invalid email or password."));
    }

    [Test]
    public async Task OnPostAsync_SystemUserCredentials_IssuesCookieAndSkipsService()
    {
        var loginService = new RecordingLoginService();
        var httpContext = new DefaultHttpContext();

        var options = new SystemUserOptions
        {
            Enabled = true,
            SessionTimeout = TimeSpan.FromHours(12),
            Users = new List<SystemUserCredential>
            {
                new()
                {
                    UserName = "Admin",
                    Password = "SuperSecret!"
                }
            }
        };

        var model = CreateModel(loginService, httpContext, options);
        model.Input.Email = "Admin";
        model.Input.Password = "SuperSecret!";

        var result = await model.OnPostAsync();

        Assert.That(result, Is.TypeOf<RedirectToPageResult>());
        Assert.That(loginService.CallCount, Is.EqualTo(0));

        var setCookies = httpContext.Response.Headers.SetCookie.ToString();
        Assert.That(setCookies, Does.Contain($"{AuthenticationConfiguration.AccessTokenKey}="));
        Assert.That(setCookies, Does.Not.Contain($"{AuthenticationConfiguration.RefreshTokenKey}="));
    }

    [Test]
    public async Task OnPostAsync_SystemUserInvalidPassword_ReturnsErrorWithoutCallingService()
    {
        var loginService = new RecordingLoginService();
        var httpContext = new DefaultHttpContext();

        var options = new SystemUserOptions
        {
            Enabled = true,
            Users = new List<SystemUserCredential>
            {
                new()
                {
                    UserName = "Admin",
                    Password = "SuperSecret!"
                }
            }
        };

        var model = CreateModel(loginService, httpContext, options);
        model.Input.Email = "Admin";
        model.Input.Password = "WrongPassword";

        var result = await model.OnPostAsync();

        Assert.That(result, Is.TypeOf<PageResult>());
        Assert.That(loginService.CallCount, Is.EqualTo(0));
        Assert.That(model.ModelState[string.Empty]?.Errors.Single().ErrorMessage, Is.EqualTo("Invalid email or password."));
        Assert.That(model.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    private static LoginModel CreateModel(
        ILoginService loginService,
        DefaultHttpContext? httpContext = null,
        SystemUserOptions? options = null,
        ISecurityTokenGenerator? tokenGenerator = null,
        IClock? clock = null)
    {
        var context = httpContext ?? new DefaultHttpContext();
        var services = new ServiceCollection();

        var optionsWrapper = Options.Create(options ?? new SystemUserOptions());
        services.AddSingleton<IOptions<SystemUserOptions>>(optionsWrapper);

        context.RequestServices = services.BuildServiceProvider();

        var pageContext = new PageContext
        {
            HttpContext = context,
            RouteData = new RouteData(),
            ActionDescriptor = new CompiledPageActionDescriptor()
        };

        var model = new LoginModel(
            loginService,
            optionsWrapper,
            tokenGenerator ?? new RecordingSecurityTokenGenerator(),
            clock ?? new FixedClock(SystemClock.Instance.GetCurrentInstant()))
        {
            PageContext = pageContext
        };

        return model;
    }

    private sealed class RecordingLoginService : ILoginService
    {
        public LoginServiceResult Result { get; set; } = new() { Success = true };

        public int CallCount { get; private set; }

        public string? LastEmail { get; private set; }

        public string? LastPassword { get; private set; }

        public HttpContext? LastHttpContext { get; private set; }

        public Task<LoginServiceResult> AuthenticateAsync(string email, string password, HttpContext httpContext, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastEmail = email;
            LastPassword = password;
            LastHttpContext = httpContext;

            return Task.FromResult(Result);
        }
    }

    private sealed class RecordingSecurityTokenGenerator : ISecurityTokenGenerator
    {
        public SecurityTokenDescriptor? LastDescriptor { get; private set; }

        public SecurityToken Generate(SecurityTokenDescriptor securityTokenDescriptor)
        {
            LastDescriptor = securityTokenDescriptor;
            return new JwtSecurityToken(
                issuer: securityTokenDescriptor.Issuer,
                audience: securityTokenDescriptor.Audience,
                claims: securityTokenDescriptor.Subject?.Claims,
                notBefore: securityTokenDescriptor.NotBefore,
                expires: securityTokenDescriptor.Expires,
                signingCredentials: securityTokenDescriptor.SigningCredentials);
        }

        public string GenerateJwt(GE.BandSite.Database.User user, HostString host)
        {
            return "token";
        }
    }

    private sealed class FixedClock : IClock
    {
        private readonly Instant _now;

        public FixedClock(Instant now)
        {
            _now = now;
        }

        public Instant GetCurrentInstant() => _now;
    }
}

using System.Threading;
using System.Threading.Tasks;
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

    private static LoginModel CreateModel(ILoginService loginService, DefaultHttpContext? httpContext = null)
    {
        var context = httpContext ?? new DefaultHttpContext();
        var pageContext = new PageContext
        {
            HttpContext = context,
            RouteData = new RouteData(),
            ActionDescriptor = new CompiledPageActionDescriptor()
        };

        var model = new LoginModel(loginService)
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
}

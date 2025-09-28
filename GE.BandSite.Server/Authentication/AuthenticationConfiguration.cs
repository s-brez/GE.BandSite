namespace GE.BandSite.Server.Authentication;

public class AuthenticationConfiguration
{
    public const int AccessTokenExpirationMinutes = 15;
    public const string AccessTokenKey = "access_token";

    public const string LoginPath = "/Login";
    public const string RedirectQueryKey = "redirect";

    public const int RefreshTokenExpirationDays = 7;
    public const string RefreshTokenKey = "refresh_token";
}
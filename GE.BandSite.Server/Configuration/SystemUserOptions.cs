using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace GE.BandSite.Server.Configuration;

/// <summary>
/// Defines options for configuration-backed system users.
/// </summary>
public sealed class SystemUserOptions
{
    /// <summary>
    /// Enables the configuration-backed system user bypass.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Lifetime for issued system user sessions.
    /// </summary>
    public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromMinutes(Authentication.AuthenticationConfiguration.AccessTokenExpirationMinutes);

    /// <summary>
    /// Configured system user credentials.
    /// </summary>
    public List<SystemUserCredential> Users { get; set; } = new();

    /// <summary>
    /// Attempts to retrieve a system user matching the supplied username.
    /// </summary>
    public bool TryGetUser(string? userName, [NotNullWhen(true)] out SystemUserCredential? credential)
    {
        credential = null;
        if (!Enabled || string.IsNullOrWhiteSpace(userName))
        {
            return false;
        }

        credential = Users.FirstOrDefault(
            x => string.Equals(x.UserName, userName, StringComparison.OrdinalIgnoreCase));

        return credential != null;
    }
}

/// <summary>
/// Represents a configuration-backed system user credential.
/// </summary>
public sealed class SystemUserCredential
{
    /// <summary>
    /// Username entered by the admin on the login page.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Plain-text password stored in configuration.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}

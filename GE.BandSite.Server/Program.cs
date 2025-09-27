using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SecurityToken;
using Amazon.SimpleEmailV2;
using GE.BandSite.Database;
using GE.BandSite.Database.Configuration;
using GE.BandSite.Server.Authentication;
using GE.BandSite.Server.Configuration;
using GE.BandSite.Server.Features.Contact;
using GE.BandSite.Server.Features.Media;
using GE.BandSite.Server.Features.Media.Admin;
using GE.BandSite.Server.Features.Media.Processing;
using GE.BandSite.Server.Features.Media.Storage;
using GE.BandSite.Server.Features.Operations.Backups;
using GE.BandSite.Server.Features.Organization;
using GE.BandSite.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NodaTime;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace GE.BandSite.Server;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder appBuilder = WebApplication.CreateBuilder(args);

        IConfiguration configuration = appBuilder.Configuration;

        var loggingConfiguration = new LoggingConfiguration();
        configuration.GetSection("Logging").Bind(loggingConfiguration);

        try
        {
            var seedUserConfiguration = configuration.GetSection("SeedUsers")?
                .Get<List<SeedUserConfiguration>>()
                ?.ToDictionary(x => x.Email, x => x) ?? new();

            Constants.SetSystemUserConfiguration(seedUserConfiguration);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load system user data");
            Constants.SetSystemUserConfiguration(null);
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
#if DEBUG
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
#endif
            .WriteTo.File(new CompactJsonFormatter(),
                path: "Logs/ge_band_site_.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: loggingConfiguration.RetainedFileCount)
            .CreateLogger();

        appBuilder.Host.UseSerilog();

        appBuilder.Services
            .AddDbContext<GeBandSiteDbContext>((sp, dbContextOptionsBuilder) =>
            {
                dbContextOptionsBuilder.UseNpgsql(configuration.GetConnectionString("Database"), npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(Program).Assembly.FullName);
                    npgsqlOptions.UseNodaTime();
                })
#if DEBUG
                .EnableDetailedErrors().EnableSensitiveDataLogging();
#else
                    .EnableDetailedErrors();
#endif
            });

        appBuilder.Services.AddScoped<IGeBandSiteDbContext>(x => x.GetRequiredService<GeBandSiteDbContext>());

        appBuilder.Services
            .AddTransient(serviceProvider =>
            {
                AwsConfiguration aWSConfiguration = new();
                configuration.GetSection("AWS").Bind(aWSConfiguration);
                
                return aWSConfiguration;
            });

        appBuilder.Services
            .AddSingleton(serviceProvider =>
            {
                var RSAConfiguration = new RSAConfiguration();
                var RSAConfigurationSection = configuration.GetSection("Authentication").GetSection("RSA");
                RSAConfigurationSection.Bind(RSAConfiguration);

                return RSAConfiguration;
            });

        appBuilder.Services
            .AddTransient<ISecurityTokenValidator>(serviceProvider =>
            {
                var RSAConfiguration = serviceProvider.GetRequiredService<RSAConfiguration>();

                return new RsaSecurityTokenValidator(RSAConfiguration.ToParameters());
            });

        appBuilder.Services.AddTransient<IPasswordValidator, PasswordValidator>();

        appBuilder.Services
            .AddTransient<ISecurityTokenGenerator, RSASHA512JWTGenerator>(serviceProvider =>
            {
                var RSAConfiguration = serviceProvider.GetRequiredService<RSAConfiguration>();

                return new RSASHA512JWTGenerator(RSAConfiguration.ToParameters());
            });

        appBuilder.Services
            .AddTransient<IRefreshTokenGenerator, RefreshTokenGenerator>(serviceProvider =>
            {
                return new RefreshTokenGenerator();
            });

        var requestLoggingConfig = configuration.GetSection("RequestLogging").Get<RequestLoggingConfiguration>() ?? new RequestLoggingConfiguration();
        appBuilder.Services.AddSingleton(requestLoggingConfig);

        appBuilder.Services.AddSingleton<IPasswordHasher, PBKDF2SHA512PasswordHasher>();

        appBuilder.Services.AddSingleton<IClock>(SystemClock.Instance);

        appBuilder.Services.AddSingleton<IValidateOptions<DatabaseBackupOptions>, DatabaseBackupOptionsValidator>();
        appBuilder.Services.Configure<ContactNotificationOptions>(configuration.GetSection("ContactNotifications"));
        appBuilder.Services.Configure<MediaDeliveryOptions>(configuration.GetSection("MediaDelivery"));
        appBuilder.Services.Configure<MediaStorageOptions>(configuration.GetSection("MediaStorage"));
        appBuilder.Services.Configure<DatabaseBackupOptions>(configuration.GetSection("DatabaseBackup"));

        var awsConfig = appBuilder.Configuration.GetSection("AWS").Get<AwsConfiguration>()!;
        var region = RegionEndpoint.GetBySystemName(awsConfig.Region);

        // keep your current SES/S3 singletons...
        appBuilder.Services.AddSingleton<IAmazonSimpleEmailServiceV2>(_ =>
            new AmazonSimpleEmailServiceV2Client(
                new BasicAWSCredentials(awsConfig.AccessKey, awsConfig.SecretKey),
                new AmazonSimpleEmailServiceV2Config { RegionEndpoint = region, Timeout = TimeSpan.FromSeconds(8) }));

        appBuilder.Services.AddSingleton<IAmazonS3>(_ =>
            new AmazonS3Client(
                new BasicAWSCredentials(awsConfig.AccessKey, awsConfig.SecretKey),
                new AmazonS3Config { RegionEndpoint = region, Timeout = TimeSpan.FromSeconds(8) }));

        // add STS for a cheap identity/auth check
        appBuilder.Services.AddSingleton<IAmazonSecurityTokenService>(_ =>
            new AmazonSecurityTokenServiceClient(
                new BasicAWSCredentials(awsConfig.AccessKey, awsConfig.SecretKey),
                new AmazonSecurityTokenServiceConfig { RegionEndpoint = region, Timeout = TimeSpan.FromSeconds(8) }));

        // expose your AwsConfiguration as a singleton too (you already bind it elsewhere)
        appBuilder.Services.AddSingleton(awsConfig);

        appBuilder.Services.AddScoped<IMediaStorageService, MediaStorageService>();
        appBuilder.Services.AddSingleton<IDatabaseBackupProcess, PgDumpDatabaseBackupProcess>();
        appBuilder.Services.AddSingleton<IDatabaseBackupStorage, S3DatabaseBackupStorage>();
        appBuilder.Services.AddSingleton<IDatabaseBackupCoordinator, DatabaseBackupCoordinator>();

        appBuilder.Services.AddSingleton<IContactSubmissionNotifier, SesContactSubmissionNotifier>();
        appBuilder.Services.AddScoped<IContactSubmissionService, ContactSubmissionService>();
        appBuilder.Services.AddScoped<IMediaQueryService, MediaQueryService>();
        appBuilder.Services.AddScoped<IMediaAdminService, MediaAdminService>();
        appBuilder.Services.AddScoped<IOrganizationContentService, OrganizationContentService>();
        appBuilder.Services.AddScoped<IOrganizationAdminService, OrganizationAdminService>();
        appBuilder.Services.AddSingleton<IMediaTranscoder, FfmpegMediaTranscoder>();
        appBuilder.Services.AddScoped<IMediaProcessingCoordinator, MediaProcessingCoordinator>();
        appBuilder.Services.AddHostedService<MediaProcessingHostedService>();

        appBuilder.Services.AddHostedService<DatabaseBackupHostedService>();

        appBuilder.Services.AddTransient<ILoginService, LoginService>();

        appBuilder.Services.AddCors();

        appBuilder.Services
            .AddControllers()
            .AddNewtonsoftJson();

        appBuilder.Services.AddRazorPages();

        appBuilder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });

        WebApplication app = appBuilder.Build();

        // Eager-initialize singletons (no side effects expected)
        foreach (var sd in appBuilder.Services.Where(s => s.Lifetime == ServiceLifetime.Singleton))
        {
            if (sd.ServiceType.IsGenericTypeDefinition) continue;                 // open generic
            if (sd.ServiceType == typeof(IEnumerable<>)) continue;                // meta
            if (typeof(IHostedService).IsAssignableFrom(sd.ServiceType)) continue;// will be started by host
            _ = app.Services.GetRequiredService(sd.ServiceType);
        }

        // Optionally probe scoped/transient constructions (non-eager usage check)
        using (var scope = app.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            foreach (var sd in appBuilder.Services.Where(s =>
                         s.Lifetime == ServiceLifetime.Scoped || s.Lifetime == ServiceLifetime.Transient))
            {
                if (sd.ServiceType.IsGenericTypeDefinition) continue;
                if (sd.ServiceType == typeof(IEnumerable<>)) continue;
                if (typeof(IHostedService).IsAssignableFrom(sd.ServiceType)) continue;

                try { _ = sp.GetService(sd.ServiceType); } // use GetService to avoid fatal if optional
                catch (Exception ex)
                {
                    // log + rethrow if you want to fail fast
                    Log.Error(ex, "Failed to construct {Service}", sd.ServiceType);
                    throw;
                }
            }
        }

        var requestLoggingConfiguration = app.Services.GetRequiredService<RequestLoggingConfiguration>();
        if (requestLoggingConfiguration.Enabled)
        {
            app.UseSerilogRequestLogging(options =>
            {
                options.MessageTemplate = "HTTP {RequestMethod} {RequestPathSanitized} responded {StatusCode} in {Elapsed:0.0000} ms";
                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    var path = httpContext.Request.Path.Value ?? string.Empty;
                    var sanitizedPath = path;

                    if (!string.IsNullOrEmpty(path) && requestLoggingConfiguration.MaskPublicTrackingTokens)
                    {
                        sanitizedPath = SanitizePath(path);
                    }

                    diagnosticContext.Set("RequestPathSanitized", sanitizedPath);

                    if (requestLoggingConfiguration.IncludeHeaders)
                    {
                        var agent = httpContext.Request.Headers["User-Agent"].ToString();
                        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                        diagnosticContext.Set("UserAgent", agent);
                        diagnosticContext.Set("ClientIp", ip);

                        var referer = httpContext.Request.Headers["Referer"].ToString();
                        if (!string.IsNullOrEmpty(referer) && requestLoggingConfiguration.MaskPublicTrackingTokens)
                        {
                            try
                            {
                                var uri = new Uri(referer, UriKind.RelativeOrAbsolute);
                                referer = uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath + uri.Query + uri.Fragment;
                            }
                            catch { /* ignore malformed referers */ }
                        }
                        if (!string.IsNullOrEmpty(referer))
                        {
                            diagnosticContext.Set("Referer", referer);
                        }
                    }
                };
            });
        }

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseMiddleware<JWTTokenValidationMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseCors(options => options
            .SetIsOriginAllowed(_ => true)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());

        app.MapControllers();
        app.MapRazorPages();

        app.Lifetime.ApplicationStopped.Register(Log.CloseAndFlush);

        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<GeBandSiteDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            dbContext.Database.EnsureCreated();

            if (dbContext.Database.IsRelational())
            {
                dbContext.Database.Migrate();
            }

            if (!dbContext.Users.Any())
            {
                dbContext.Users.AddRange(Constants.SystemUsers);
                dbContext.SaveChanges();
            }

            MediaSeedData.EnsureSeedDataAsync(dbContext, clock).GetAwaiter().GetResult();
            OrganizationSeedData.EnsureSeedDataAsync(dbContext, clock).GetAwaiter().GetResult();
        }

        app.Run();

    }

    private static string SanitizePath(string path)
    {
        return System.Text.RegularExpressions.Regex.Replace(path, "[A-Fa-f0-9]{8,}-[A-Fa-f0-9-]{13,}", "***");
    }
}

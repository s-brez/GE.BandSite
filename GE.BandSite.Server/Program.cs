using Amazon;
using System.Linq;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
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
using GE.BandSite.Server.Services.Processes;
using GE.BandSite.Server.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using NodaTime;
using Npgsql;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using System.Data;

namespace GE.BandSite.Server;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder appBuilder = WebApplication.CreateBuilder(args);

        IConfiguration configuration = appBuilder.Configuration;

        var loggingConfiguration = LoadLoggingConfiguration(configuration);

        LoadSeedUsers(configuration);

        ConfigureSerilog(appBuilder, loggingConfiguration);

        RegisterDatabaseServices(appBuilder, configuration);

        RegisterAuthenticationServices(appBuilder, configuration);

        RegisterInfrastructureServices(appBuilder, configuration);

        RegisterAwsServices(appBuilder, configuration);

        RegisterFeatureServices(appBuilder);

        RegisterHostedServices(appBuilder);

        RegisterPresentationLayer(appBuilder);

        ConfigureServiceProvider(appBuilder);

        WebApplication app = appBuilder.Build();

        WarmAwsClients(app.Services);

        EagerInitializeSingletons(appBuilder.Services, app.Services);

        ValidateServiceConstructions(appBuilder.Services, app.Services);

        ConfigureApplication(app);

        EnsureDatabasePrepared(app.Services);

        app.Run();
    }

    private static LoggingConfiguration LoadLoggingConfiguration(IConfiguration configuration)
    {
        var loggingConfiguration = new LoggingConfiguration();
        configuration.GetSection("Logging").Bind(loggingConfiguration);
        return loggingConfiguration;
    }

    private static void LoadSeedUsers(IConfiguration configuration)
    {
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
    }

    private static void ConfigureSerilog(WebApplicationBuilder builder, LoggingConfiguration loggingConfiguration)
    {
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

        builder.Host.UseSerilog();
    }

    private static void RegisterDatabaseServices(WebApplicationBuilder builder, IConfiguration configuration)
    {
        builder.Services
            .AddDbContext<GeBandSiteDbContext>((_, dbContextOptionsBuilder) =>
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

        builder.Services.AddScoped<IGeBandSiteDbContext>(x => x.GetRequiredService<GeBandSiteDbContext>());
    }

    private static void RegisterAuthenticationServices(WebApplicationBuilder builder, IConfiguration configuration)
    {
        builder.Services
            .AddSingleton(_ =>
            {
                var rsaConfiguration = new RSAConfiguration();
                configuration.GetSection("Authentication").GetSection("RSA").Bind(rsaConfiguration);
                return rsaConfiguration;
            });

        builder.Services
            .AddTransient<ISecurityTokenValidator>(serviceProvider =>
            {
                var rsaConfiguration = serviceProvider.GetRequiredService<RSAConfiguration>();
                return new RsaSecurityTokenValidator(rsaConfiguration.ToParameters());
            });

        builder.Services.AddTransient<IPasswordValidator, PasswordValidator>();

        builder.Services
            .AddTransient<ISecurityTokenGenerator, RSASHA512JWTGenerator>(serviceProvider =>
            {
                var rsaConfiguration = serviceProvider.GetRequiredService<RSAConfiguration>();
                return new RSASHA512JWTGenerator(rsaConfiguration.ToParameters());
            });

        builder.Services
            .AddTransient<IRefreshTokenGenerator, RefreshTokenGenerator>(_ => new RefreshTokenGenerator());

        builder.Services.AddSingleton<IPasswordHasher, PBKDF2SHA512PasswordHasher>();
        builder.Services.Configure<SystemUserOptions>(configuration.GetSection("SystemUser"));
        builder.Services.AddTransient<ILoginService, LoginService>();
    }

    private static void RegisterInfrastructureServices(WebApplicationBuilder builder, IConfiguration configuration)
    {
        var requestLoggingConfig = configuration.GetSection("RequestLogging").Get<RequestLoggingConfiguration>() ?? new RequestLoggingConfiguration();
        builder.Services.AddSingleton(requestLoggingConfig);

        builder.Services.AddSingleton<IClock>(SystemClock.Instance);
        builder.Services.AddSingleton<IValidateOptions<DatabaseBackupOptions>, DatabaseBackupOptionsValidator>();
        builder.Services.AddSingleton<IValidateOptions<MediaProcessingOptions>, MediaProcessingOptionsValidator>();
        builder.Services.AddSingleton<IValidateOptions<MediaDeliveryOptions>, MediaDeliveryOptionsValidator>();

        builder.Services.Configure<ContactNotificationOptions>(configuration.GetSection("ContactNotifications"));
        builder.Services.Configure<PasswordResetOptions>(configuration.GetSection("PasswordReset"));
        builder.Services.Configure<MediaDeliveryOptions>(configuration.GetSection("MediaDelivery"));
        builder.Services.Configure<MediaStorageOptions>(configuration.GetSection("MediaStorage"));
        builder.Services.Configure<DatabaseBackupOptions>(configuration.GetSection("DatabaseBackup"));

        builder.Services.PostConfigure<ContactNotificationOptions>(options =>
        {
            var enabledValue = configuration["CONTACT_NOTIFICATIONS_ENABLED"];
            if (!string.IsNullOrWhiteSpace(enabledValue) && bool.TryParse(enabledValue, out var enabled))
            {
                options.Enabled = enabled;
            }

            var fromAddress = configuration["CONTACT_NOTIFICATIONS_FROM_ADDRESS"];
            if (string.IsNullOrWhiteSpace(options.FromAddress) && !string.IsNullOrWhiteSpace(fromAddress))
            {
                options.FromAddress = fromAddress.Trim();
            }

            var subject = configuration["CONTACT_NOTIFICATIONS_SUBJECT"];
            if (string.IsNullOrWhiteSpace(options.Subject) && !string.IsNullOrWhiteSpace(subject))
            {
                options.Subject = subject.Trim();
            }

            var recipientsValue = configuration["CONTACT_NOTIFICATIONS_RECIPIENTS"];
            if (options.ToAddresses.Count == 0 && !string.IsNullOrWhiteSpace(recipientsValue))
            {
                var recipients = recipientsValue
                    .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(static value => value.Trim())
                    .Where(static value => value.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (recipients.Count > 0)
                {
                    options.ToAddresses = recipients;
                }
            }
        });
    }

    private static void RegisterAwsServices(WebApplicationBuilder builder, IConfiguration configuration)
    {
        var awsConfig = AwsConfiguration.FromConfiguration(configuration);

        builder.Services.AddSingleton(_ => awsConfig);
        var region = RegionEndpoint.GetBySystemName(awsConfig.Region);

        builder.Services.AddSingleton<IAmazonSimpleEmailServiceV2>(_ =>
            new AmazonSimpleEmailServiceV2Client(
                new BasicAWSCredentials(awsConfig.AccessKey, awsConfig.SecretKey),
                new AmazonSimpleEmailServiceV2Config { RegionEndpoint = region, Timeout = TimeSpan.FromSeconds(10) }));

        builder.Services.AddSingleton<IAmazonS3>(_ =>
            new AmazonS3Client(
                new BasicAWSCredentials(awsConfig.AccessKey, awsConfig.SecretKey),
                new AmazonS3Config { RegionEndpoint = region, Timeout = TimeSpan.FromMinutes(15) }));

        builder.Services.AddSingleton<IAmazonSecurityTokenService>(_ =>
            new AmazonSecurityTokenServiceClient(
                new BasicAWSCredentials(awsConfig.AccessKey, awsConfig.SecretKey),
                new AmazonSecurityTokenServiceConfig { RegionEndpoint = region, Timeout = TimeSpan.FromSeconds(10) }));

        builder.Services.AddSingleton(awsConfig);
        builder.Services.AddSingleton<IS3Client, AwsS3Client>();
        builder.Services.AddSingleton<IExternalProcessRunner, ExternalProcessRunner>();
    }

    private static void RegisterFeatureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IMediaStorageService, MediaStorageService>();
        builder.Services.AddSingleton<IDatabaseBackupProcess, PgDumpDatabaseBackupProcess>();
        builder.Services.AddSingleton<IDatabaseBackupStorage, S3DatabaseBackupStorage>();
        builder.Services.AddSingleton<IDatabaseBackupCoordinator, DatabaseBackupCoordinator>();

        builder.Services.AddSingleton<ISesEmailClient, SesEmailClient>();
        builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
        builder.Services.AddScoped<IContactNotificationSettingsService, ContactNotificationSettingsService>();
        builder.Services.AddScoped<IContactNotificationRecipientProvider>(sp => sp.GetRequiredService<IContactNotificationSettingsService>());
        builder.Services.AddScoped<IContactSubmissionNotifier, SesContactSubmissionNotifier>();
        builder.Services.AddScoped<IContactSubmissionService, ContactSubmissionService>();
        builder.Services.AddScoped<IMediaQueryService, MediaQueryService>();
        builder.Services.AddScoped<IMediaAdminService, MediaAdminService>();
        builder.Services.AddScoped<IImageOptimizer, ImageSharpImageOptimizer>();
        builder.Services.AddScoped<IOrganizationContentService, OrganizationContentService>();
        builder.Services.AddScoped<IOrganizationAdminService, OrganizationAdminService>();
        builder.Services.AddSingleton<IMediaTranscoder, FfmpegMediaTranscoder>();
        builder.Services.AddScoped<IMediaProcessingCoordinator, MediaProcessingCoordinator>();
        builder.Services.AddScoped<MediaStorageBootstrapper>();
    }

    private static void RegisterHostedServices(WebApplicationBuilder builder)
    {
        builder.Services.AddHostedService<MediaProcessingHostedService>();
        builder.Services.AddHostedService<DatabaseBackupHostedService>();
    }

    private static void RegisterPresentationLayer(WebApplicationBuilder builder)
    {
        builder.Services.AddCors();

        builder.Services
            .AddControllers()
            .AddNewtonsoftJson();

        builder.Services.AddRazorPages();
    }

    private static void ConfigureServiceProvider(WebApplicationBuilder builder)
    {
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });
    }

    private static void ConfigureApplication(WebApplication app)
    {
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
                            catch
                            {
                                // ignore malformed referers
                            }
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
    }

    private static void EnsureDatabasePrepared(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GeBandSiteDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        if (dbContext.Database.IsRelational())
        {
            EnsureRelationalDatabasePrepared(dbContext);
        }
        else
        {
            var created = dbContext.Database.EnsureCreated();
            Log.Information("EnsureCreated completed for non-relational provider; databaseCreated={DatabaseCreated}", created);
        }

        if (!dbContext.Users.Any())
        {
            dbContext.Users.AddRange(Constants.SystemUsers);
            dbContext.SaveChanges();
        }

        var mediaBootstrapper = scope.ServiceProvider.GetRequiredService<MediaStorageBootstrapper>();
        mediaBootstrapper.EnsureAsync().GetAwaiter().GetResult();

        OrganizationSeedData.EnsureSeedDataAsync(dbContext, clock).GetAwaiter().GetResult();
    }

    private static void EnsureDatabaseSchemas(GeBandSiteDbContext dbContext)
    {
        var schemas = new[]
        {
            Schemas.Organization,
            Schemas.Authentication,
            Schemas.Media
        };

        foreach (var schema in schemas)
        {
            var command = $"CREATE SCHEMA IF NOT EXISTS \"{schema}\"";
            Log.Information("Ensuring PostgreSQL schema {Schema}", schema);
            dbContext.Database.ExecuteSqlRaw(command);
        }
    }

    private static void EnsureRelationalDatabasePrepared(GeBandSiteDbContext dbContext)
    {
        var databaseCreator = dbContext.Database.GetService<IRelationalDatabaseCreator>();

        if (!databaseCreator.Exists())
        {
            var databaseName = dbContext.Database.GetDbConnection().Database;
            Log.Information("Database {DatabaseName} does not exist; creating", databaseName);
            databaseCreator.Create();
        }

        EnsureDatabaseSchemas(dbContext);

        var databaseCreated = dbContext.Database.EnsureCreated();
        Log.Information("EnsureCreated completed; databaseCreated={DatabaseCreated}", databaseCreated);

        var missingTables = GetMissingTables(dbContext);
        if (missingTables.Count == 0)
        {
            Log.Information("All EF Core tables detected after EnsureCreated");
            return;
        }

        Log.Warning("Missing tables detected after EnsureCreated; attempting CreateTables: {MissingTables}", string.Join(", ", missingTables));

        try
        {
            databaseCreator.CreateTables();
        }
        catch (PostgresException exception) when (string.Equals(exception.SqlState, PostgresErrorCodes.DuplicateTable, StringComparison.Ordinal))
        {
            Log.Warning(exception, "CreateTables reported duplicate tables; continuing");
        }

        missingTables = GetMissingTables(dbContext);
        if (missingTables.Count > 0)
        {
            throw new InvalidOperationException($"Failed to create required tables: {string.Join(", ", missingTables)}");
        }

        Log.Information("All EF Core tables detected after CreateTables");
    }

    private static IReadOnlyCollection<string> GetMissingTables(GeBandSiteDbContext dbContext)
    {
        var entityTables = dbContext.Model
            .GetEntityTypes()
            .Where(entityType => !entityType.IsOwned())
            .Select(entityType => new
            {
                Schema = entityType.GetSchema() ?? dbContext.Model.GetDefaultSchema() ?? "public",
                Table = entityType.GetTableName()
            })
            .Where(tuple => !string.IsNullOrWhiteSpace(tuple.Table))
            .Distinct()
            .ToList();

        var missing = new List<string>();

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            connection.Open();
        }

        try
        {
            foreach (var table in entityTables)
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = @schema AND table_name = @table)";

                var schemaParameter = command.CreateParameter();
                schemaParameter.ParameterName = "@schema";
                schemaParameter.Value = table.Schema;
                command.Parameters.Add(schemaParameter);

                var tableParameter = command.CreateParameter();
                tableParameter.ParameterName = "@table";
                tableParameter.Value = table.Table!;
                command.Parameters.Add(tableParameter);

                var exists = command.ExecuteScalar() as bool? ?? false;
                if (!exists)
                {
                    missing.Add($"{table.Schema}.{table.Table}");
                }
            }
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }

        return missing;
    }

    private static void EagerInitializeSingletons(IServiceCollection services, IServiceProvider provider)
    {
        foreach (var descriptor in services.Where(s => s.Lifetime == ServiceLifetime.Singleton))
        {
            if (descriptor.ServiceType.IsGenericTypeDefinition)
            {
                continue;
            }

            if (descriptor.ServiceType == typeof(IEnumerable<>))
            {
                continue;
            }

            if (typeof(IHostedService).IsAssignableFrom(descriptor.ServiceType))
            {
                continue;
            }

            _ = provider.GetRequiredService(descriptor.ServiceType);
        }
    }

    private static void ValidateServiceConstructions(IServiceCollection services, IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        foreach (var descriptor in services.Where(s => s.Lifetime == ServiceLifetime.Scoped || s.Lifetime == ServiceLifetime.Transient))
        {
            if (descriptor.ServiceType.IsGenericTypeDefinition)
            {
                continue;
            }

            if (descriptor.ServiceType == typeof(IEnumerable<>))
            {
                continue;
            }

            if (typeof(IHostedService).IsAssignableFrom(descriptor.ServiceType))
            {
                continue;
            }

            try
            {
                _ = scopedProvider.GetService(descriptor.ServiceType);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Failed to construct {Service}", descriptor.ServiceType);
                throw;
            }
        }
    }

    private static string SanitizePath(string path)
    {
        return System.Text.RegularExpressions.Regex.Replace(path, "[A-Fa-f0-9]{8,}-[A-Fa-f0-9-]{13,}", "***");
    }

    private static void WarmAwsClients(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cancellationToken = cancellationTokenSource.Token;

        var sesClient = scope.ServiceProvider.GetRequiredService<IAmazonSimpleEmailServiceV2>();
        var s3Client = scope.ServiceProvider.GetRequiredService<IAmazonS3>();
        var stsClient = scope.ServiceProvider.GetRequiredService<IAmazonSecurityTokenService>();

        try
        {
            Task.WhenAll(
                sesClient.GetAccountAsync(new GetAccountRequest(), cancellationToken),
                s3Client.ListBucketsAsync(cancellationToken),
                stsClient.GetCallerIdentityAsync(new GetCallerIdentityRequest(), cancellationToken)).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to verify AWS credentials during startup.");
            throw;
        }
    }
}

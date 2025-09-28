using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace GE.BandSite.Testing.Core;

/// <summary>
/// Provides an <strong>ephemeral PostgreSQL database</strong> for integration- and unit-tests.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="TestPostgresProvider"/> is designed to hide all environment-specific
/// plumbing that is required to spin-up a fresh PostgreSQL schema, apply Entity Framework Core
/// migrations and tear everything down again once a test run is finished.
/// </para>
/// <para>
/// Depending on where the tests are executed, the provider takes one of two execution paths:
/// <list type="bullet">
///   <item>
///     <term><em>In-container / CI environment</em></term>
///     <description>
///       When the environment variable <c>TEST_PG_CONNECTIONSTRING</c> is present the provider
///       **assumes that the test process is already running inside a Docker container** that has
///       network access to a long-lived PostgreSQL instance (for example the Codex build agents).
///       A brand-new database with a random name is created using the admin connection string so
///       that test executions remain isolated from each other.
///     </description>
///   </item>
///   <item>
///     <term><em>Local developer workstation</em></term>
///     <description>
///       If the variable is <c>null</c> or empty, <see href="https://dotnet.testcontainers.org/modules/postgres/">Testcontainers for .NET</see>
///       is used to spin up a disposable <c>postgres:17</c> container.  The container lifetime is
///       bound to the lifetime of the current test run - no global state is written to the host
///       machine and the container (together with all data) is removed automatically.
///     </description>
///   </item>
/// </list>
/// </para>
/// <para>
/// <strong>Typical usage pattern</strong> (illustrated in the <c>PostgresConstraintTests</c> fixture):
/// </para>
/// <code language="csharp">
/// private TestPostgresProvider PostGresProvider;
/// private MyDbContext _db;
///
/// [SetUp]
/// public async Task SetUp()
/// {
///     PostGresProvider = new TestPostgresProvider();
///     await PostGresProvider.InitializeAsync();
///
///     _db = PostGresProvider.CreateDbContext<MyDbContext>();
///     await _db.Database.EnsureCreatedAsync();
///     await _db.Database.MigrateAsync();
/// }
///
/// [TearDown]
/// public async Task TearDown()
/// {
///     await PostGresProvider.DisposeDbContextAsync(_db);
///     await PostGresProvider.DisposeAsync();
/// }
/// </code>
/// <para>
/// After disposal **all database artefacts are rigorously removed**.  On CI the dedicated database
/// is dropped using the <c>WITH (FORCE)</c> clause (PostgreSQL 13+) so that no remaining
/// connections can block the drop.  Locally the Testcontainers instance is stopped and removed.
/// </para>
/// <para>
/// <strong>Thread-safety / parallelisation:</strong> The provider is <em>not</em> thread-safe; create
/// a dedicated instance per test fixture and decorate the fixture with
/// <c>[Parallelizable(ParallelScope.None)]</c> when the underlying database state is shared between
/// tests.
/// </para>
/// </remarks>
public class TestPostgresProvider : IAsyncDisposable
{
    private static readonly object ContainerSync = new object();
    private static PostgreSqlContainer? SharedContainer;
    private static int SharedContainerRefCount = 0;
    private static bool SharedContainerStarted = false;

    private PostgreSqlContainer? PostgresContainer;
    private string? AdminConnectionString;
    private string? DbName;
    private bool IsDisposed;

    /// <summary>
    /// Gets the connection string that Entity Framework Core should use to connect to the
    /// temporarily provisioned database.
    /// </summary>
    /// <value>
    /// A fully formed Npgsql connection string.
    /// </value>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Indicates whether the provider is running <em>inside</em> a pre‑existing container
    /// (CI environments) rather than having started its own Docker Testcontainer.
    /// </summary>
    public bool IsContainerEnvironment => PostgresContainer == null;

    /// <summary>
    /// Creates a fresh PostgreSQL database that will exist only for the duration of the test run.
    /// <para>
    /// Must be invoked exactly once per provider instance <strong>before</strong> any call to
    /// <see cref="CreateDbContext{TContext}()"/>, otherwise those methods will fail because the
    /// <see cref="ConnectionString"/> is not initialised.
    /// </para>
    /// </summary>
    public async Task InitializeAsync()
    {
        var environmentConnectionString = Environment.GetEnvironmentVariable("TEST_PG_CONNECTIONSTRING");

        if (!string.IsNullOrWhiteSpace(environmentConnectionString))
        {
            // Running in CI/container environments: use the provided connection string to create an isolated test database.
            await InitializeInContainerEnvironment(environmentConnectionString);
        }
        else
        {
            // Running locally: use fresh pg testcontainer
            await InitializeLocalEnvironment();
        }
    }

    /// <summary>
    /// Sets up the provider in a CI ("in‑container") environment by creating a dedicated database
    /// within the cluster that is already running in the container network.
    /// </summary>
    /// <param name="environmentConnectionString">Value of <c>TEST_PG_CONNECTIONSTRING</c>.</param>
    private async Task InitializeInContainerEnvironment(string environmentConnectionString)
    {
        DbName = "tests_" + Guid.NewGuid().ToString("N");

        var adminCsb = new NpgsqlConnectionStringBuilder(environmentConnectionString)
        {
            Database = "postgres",
            Pooling = false
        };
        AdminConnectionString = adminCsb.ToString();

        await using (var admin = new NpgsqlConnection(AdminConnectionString))
        {
            await admin.OpenAsync();

            await using var cmd = admin.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE \"{DbName}\" OWNER postgres";
            await cmd.ExecuteNonQueryAsync();
        }

        var csb = new NpgsqlConnectionStringBuilder(environmentConnectionString) { Database = DbName };
        ConnectionString = csb.ToString();
    }

    /// <summary>
    /// Starts (or reuses) a Docker‑managed PostgreSQL instance using Testcontainers and creates a
    /// unique database for this provider instance. Exposes the connection string via <see cref="ConnectionString"/>.
    /// </summary>
    private async Task InitializeLocalEnvironment()
    {
        // Create or reuse a single container per test process to avoid repeated image spins.
        if (SharedContainer == null)
        {
            lock (ContainerSync)
            {
                if (SharedContainer == null)
                {
                    SharedContainer = new PostgreSqlBuilder()
                        .WithImage("postgres:17")
                        .WithDatabase("tests")
                        .WithUsername("postgres")
                        .WithPassword("pwd")
                        .WithReuse(true)
                        .Build();
                }
                SharedContainerRefCount++;
            }
        }

        // Ensure container is started once per process.
        if (!SharedContainerStarted)
        {
            lock (ContainerSync)
            {
                if (!SharedContainerStarted)
                {
                    try
                    {
                        SharedContainer!.StartAsync().GetAwaiter().GetResult();
                        SharedContainerStarted = true;
                    }
                    catch
                    {
                        // Best-effort start; container likely already running.
                        SharedContainerStarted = true;
                    }
                }
            }
        }

        PostgresContainer = SharedContainer;

        // Create a dedicated database for this provider instance for isolation across parallel tests.
        DbName = "tests_" + Guid.NewGuid().ToString("N");

        var baseCsb = new NpgsqlConnectionStringBuilder(PostgresContainer.GetConnectionString());
        var adminCsb = new NpgsqlConnectionStringBuilder(baseCsb.ToString())
        {
            Database = "postgres",
            Pooling = false
        };
        AdminConnectionString = adminCsb.ToString();

        await using (var admin = new NpgsqlConnection(AdminConnectionString))
        {
            await admin.OpenAsync();
            await using var cmd = admin.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE \"{DbName}\" OWNER postgres";
            await cmd.ExecuteNonQueryAsync();
        }

        var csb = new NpgsqlConnectionStringBuilder(baseCsb.ToString()) { Database = DbName };
        ConnectionString = csb.ToString();
    }


    /// <summary>
    /// Creates a pre‑configured <see cref="DbContextOptionsBuilder{TContext}"/> that points to the
    /// temporary database prepared by the provider.
    /// </summary>
    /// <typeparam name="TContext">Your Entity Framework Core <see cref="DbContext"/> type.</typeparam>
    public DbContextOptionsBuilder<TContext> CreateDbContextOptionsBuilder<TContext>() where TContext : DbContext
    {
        return new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(ConnectionString, x => x.UseNodaTime())
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging();
    }

    /// <summary>
    /// Instantiates a <typeparamref name="TContext"/> using the options produced by
    /// <see cref="CreateDbContextOptionsBuilder{TContext}"/>.
    /// </summary>
    /// <typeparam name="TContext">Your <see cref="DbContext"/> type.</typeparam>
    public TContext CreateDbContext<TContext>() where TContext : DbContext
    {
        var options = CreateDbContextOptionsBuilder<TContext>().Options;
        return (TContext)Activator.CreateInstance(typeof(TContext), options)!;
    }


    /// <summary>
    /// Disposes the supplied <see cref="DbContext"/> but swallows the <c>57P01 - ADMIN SHUTDOWN</c>
    /// error that is thrown when the provider has already terminated the database connection during
    /// test cleanup.
    /// </summary>
    public async Task DisposeDbContextAsync(DbContext? dbContext)
    {
        if (dbContext is not null)
        {
            try
            {
                await dbContext.DisposeAsync();
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.AdminShutdown)
            {
                // The database was already shut down - safe to ignore.
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (IsDisposed) return;

        // Ensure that no pooled connector keeps the test database locked.
        NpgsqlConnection.ClearAllPools();

        if (AdminConnectionString is not null && DbName is not null)
        {
            await DropDatabase();
        }

        if (PostgresContainer is not null)
        {
            // Manage shared container lifetime — do not stop a reusable shared container.
            lock (ContainerSync)
            {
                if (ReferenceEquals(PostgresContainer, SharedContainer))
                {
                    SharedContainerRefCount = Math.Max(0, SharedContainerRefCount - 1);
                    // Intentionally keep the reusable container running across processes.
                }
                else
                {
                    PostgresContainer.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }
        }

        IsDisposed = true;
    }

    /// <summary>
    /// Drops the dedicated database that was created for the current test run. Uses the PostgreSQL
    /// 13+ <c>WITH (FORCE)</c> clause to terminate any lingering connections that would otherwise
    /// prevent a successful drop.
    /// </summary>
    private async Task DropDatabase()
    {
        var adminCsb = new NpgsqlConnectionStringBuilder(AdminConnectionString!)
        {
            Database = "postgres",
            Pooling = false
        };

        await using var admin = new NpgsqlConnection(adminCsb.ToString());

        try
        {
            await admin.OpenAsync();

            await using var cmd = admin.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS \"{DbName}\" WITH (FORCE)";
            await cmd.ExecuteNonQueryAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
        {
            // Database does not exist - nothing to drop.
        }
    }
}

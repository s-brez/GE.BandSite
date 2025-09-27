using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace GE.BandSite.Server.Features.Operations.Backups;

public sealed record DatabaseBackupProcessRequest(
    string ConnectionString,
    string PgDumpPath,
    string OutputDirectory,
    DateTimeOffset Timestamp);

public interface IDatabaseBackupProcess
{
    Task<string> CreateDumpAsync(DatabaseBackupProcessRequest request, CancellationToken cancellationToken = default);
}

public sealed class PgDumpDatabaseBackupProcess : IDatabaseBackupProcess
{
    private readonly ILogger<PgDumpDatabaseBackupProcess> _logger;

    public PgDumpDatabaseBackupProcess(ILogger<PgDumpDatabaseBackupProcess> logger)
    {
        _logger = logger;
    }

    public async Task<string> CreateDumpAsync(DatabaseBackupProcessRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.ConnectionString);
        ArgumentException.ThrowIfNullOrEmpty(request.PgDumpPath);
        ArgumentException.ThrowIfNullOrEmpty(request.OutputDirectory);

        var connection = new NpgsqlConnectionStringBuilder(request.ConnectionString);
        if (string.IsNullOrWhiteSpace(connection.Database))
        {
            throw new InvalidOperationException("Database connection string must include a database name.");
        }

        var fileName = $"ge-band-site-{request.Timestamp:yyyyMMdd-HHmmss}.dump";
        Directory.CreateDirectory(request.OutputDirectory);
        var destinationPath = Path.Combine(request.OutputDirectory, fileName);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = request.PgDumpPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        processStartInfo.ArgumentList.Add("--format=custom");
        processStartInfo.ArgumentList.Add("--file");
        processStartInfo.ArgumentList.Add(destinationPath);

        if (!string.IsNullOrWhiteSpace(connection.Host))
        {
            processStartInfo.Environment["PGHOST"] = connection.Host;
        }

        if (connection.Port > 0)
        {
            processStartInfo.Environment["PGPORT"] = connection.Port.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(connection.Username))
        {
            processStartInfo.Environment["PGUSER"] = connection.Username;
        }

        if (!string.IsNullOrWhiteSpace(connection.Password))
        {
            processStartInfo.Environment["PGPASSWORD"] = connection.Password;
        }

        processStartInfo.ArgumentList.Add(connection.Database);

        using var process = new Process
        {
            StartInfo = processStartInfo
        };

        _logger.LogInformation("Starting pg_dump backup for database {Database}.", connection.Database);

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start pg_dump process.");
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var standardOutput = await standardOutputTask.ConfigureAwait(false);
        var standardError = await standardErrorTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            _logger.LogError(
                "pg_dump exited with {ExitCode}. stderr: {ErrorOutput} stdout: {StandardOutput}",
                process.ExitCode,
                standardError,
                standardOutput);

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            throw new InvalidOperationException($"pg_dump exited with {process.ExitCode}.");
        }

        _logger.LogInformation("pg_dump backup completed successfully ({FilePath}).", destinationPath);
        return destinationPath;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // ignored
        }
    }
}

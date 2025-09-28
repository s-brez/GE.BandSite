using System.Diagnostics;

namespace GE.BandSite.Server.Services.Processes;

public interface IExternalProcessRunner
{
    Task<ExternalProcessResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default);
}

public sealed record ExternalProcessResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class ExternalProcessRunner : IExternalProcessRunner
{
    public async Task<ExternalProcessResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        using var process = new Process { StartInfo = startInfo };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start external process '{startInfo.FileName}'.");
        }

        var stdOutTask = process.StartInfo.RedirectStandardOutput
            ? process.StandardOutput.ReadToEndAsync(cancellationToken)
            : Task.FromResult(string.Empty);

        var stdErrTask = process.StartInfo.RedirectStandardError
            ? process.StandardError.ReadToEndAsync(cancellationToken)
            : Task.FromResult(string.Empty);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var stdout = await stdOutTask.ConfigureAwait(false);
        var stderr = await stdErrTask.ConfigureAwait(false);

        return new ExternalProcessResult(process.ExitCode, stdout, stderr);
    }
}

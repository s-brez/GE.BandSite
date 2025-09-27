namespace GE.BandSite.Tools;

/// <summary>
/// Provides helpers for locating solution root during test runs.
/// </summary>
public static class SolutionRootLocator
{
    private const string DefaultSolutionFileName = "GE.BandSite.sln";

    /// <summary>
    /// Walks up the directory hierarchy starting at <paramref name="startDirectory"/> (or the current AppContext base directory)
    /// until a directory containing the solution file is found.
    /// </summary>
    /// <param name="startDirectory">An optional starting directory. If not provided, <see cref="AppContext.BaseDirectory"/> is used.</param>
    /// <param name="solutionFileName">The solution filename to look for. Defaults to GE.BandSite.sln.</param>
    /// <returns>The absolute path to the solution root directory.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="solutionFileName"/> is null or whitespace.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the solution root cannot be located.</exception>
    public static string FindSolutionRoot(string? startDirectory = null, string solutionFileName = DefaultSolutionFileName)
    {
        if (string.IsNullOrWhiteSpace(solutionFileName))
        {
            throw new ArgumentException("Solution file name must be provided.", nameof(solutionFileName));
        }

        var directory = string.IsNullOrWhiteSpace(startDirectory)
            ? AppContext.BaseDirectory
            : startDirectory;

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new DirectoryNotFoundException("Could not locate solution root (GE.BandSite.sln).");
        }

        var current = new DirectoryInfo(directory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, solutionFileName)))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate solution root ({solutionFileName}).");
    }
}

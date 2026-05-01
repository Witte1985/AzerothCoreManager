using AzerothCoreManager.Core.Services.Interfaces;
using System.Diagnostics;

namespace AzerothCoreManager.Infrastructure.Services;

/// <summary>
/// Git adapter backed by the local git executable.
/// </summary>
public sealed class GitService : IGitService
{
    public async Task<bool> IsGitAvailableAsync(CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "--version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);

        return process.ExitCode == 0;
    }
}

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using AzerothCoreManager.Core.Contracts;
using AzerothCoreManager.Core.Services.Interfaces;
using AzerothCoreManager.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AzerothCoreManager.Infrastructure.Services;

/// <summary>
/// Docker adapter using direct CLI calls.
/// </summary>
public sealed class DockerService : IDockerService
{
    private readonly ILogger<DockerService> _logger;

    public DockerService(IOptions<DockerOptions> options, ILogger<DockerService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> IsDockerAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (exitCode, _) = await RunDockerCommandAsync("version --format '{{.Server.Version}}'", cancellationToken);
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<ContainerStatusDto>> ListContainersAsync(
        string? composeProjectName = null,
        CancellationToken cancellationToken = default)
    {
        var args = "ps -a --format json";
        
        if (!string.IsNullOrWhiteSpace(composeProjectName))
        {
            args += $" --filter \"label=com.docker.compose.project={composeProjectName}\"";
        }

        var (exitCode, output) = await RunDockerCommandAsync(args, cancellationToken);
        
        if (exitCode != 0)
        {
            _logger.LogWarning("docker ps command failed with exit code {ExitCode}", exitCode);
            return Array.Empty<ContainerStatusDto>();
        }

        return ParseContainerList(output);
    }

    private async Task<(int ExitCode, string Output)> RunDockerCommandAsync(
        string arguments,
        CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var outputLines = new List<string>();
        var errorLines = new List<string>();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputLines.Add(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                errorLines.Add(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return (process.ExitCode, string.Join('\n', outputLines));
    }

    private List<ContainerStatusDto> ParseContainerList(string jsonOutput)
    {
        var containers = new List<ContainerStatusDto>();
        
        if (string.IsNullOrWhiteSpace(jsonOutput))
        {
            return containers;
        }

        var lines = jsonOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            try
            {
                var container = JsonSerializer.Deserialize<DockerPsJsonOutput>(line);
                if (container != null)
                {
                    containers.Add(new ContainerStatusDto
                    {
                        Name = container.Names,
                        Status = container.State,
                        Health = ExtractHealth(container.Status),
                        StartedAt = ParseCreatedAt(container.CreatedAt)
                    });
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse container JSON: {Line}", line);
            }
        }

        return containers;
    }

    private static string ExtractHealth(string status)
    {
        if (status.Contains("(healthy)", StringComparison.OrdinalIgnoreCase))
        {
            return "healthy";
        }

        if (status.Contains("(unhealthy)", StringComparison.OrdinalIgnoreCase))
        {
            return "unhealthy";
        }

        return "unknown";
    }

    private static DateTime ParseCreatedAt(string createdAt)
    {
        // Try Unix timestamp first
        if (long.TryParse(createdAt, out var unixTimestamp))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;
        }

        // Docker returns format like "2026-04-25 01:23:04 +0200 CEST"
        // Strip the timezone abbreviation (CEST, CET, etc.) at the end
        var cleanedDate = System.Text.RegularExpressions.Regex.Replace(createdAt, @"\s+[A-Z]{3,4}$", "").Trim();

        // Try parsing as formatted date string with multiple formats
        var formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss zzz",  // "2026-04-25 01:23:04 +0200"
            "yyyy-MM-dd HH:mm:ss",       // "2026-04-25 01:23:04"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(cleanedDate, format, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var dateTime))
            {
                return dateTime.ToUniversalTime();
            }
        }

        // Fallback: try general parsing
        if (DateTime.TryParse(cleanedDate, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsedDate))
        {
            return parsedDate.ToUniversalTime();
        }

        // Last resort: return epoch time to indicate invalid/unknown
        return DateTime.UnixEpoch;
    }

    private sealed class DockerPsJsonOutput
    {
        [JsonPropertyName("Names")]
        public string Names { get; set; } = string.Empty;

        [JsonPropertyName("State")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("Status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("CreatedAt")]
        public string CreatedAt { get; set; } = string.Empty;
    }
}

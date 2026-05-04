using AzerothCoreManager.Core.Contracts;

namespace AzerothCoreManager.Infrastructure.Data.Entities;

/// <summary>
/// Persisted representation of a managed AzerothCore stack.
/// </summary>
public class ManagedStackEntity
{
    public string Id { get; set; } = string.Empty;

    public string StackName { get; set; } = string.Empty;

    public string NormalizedStackName { get; set; } = string.Empty;

    public ServerType ServerType { get; set; }

    public StackStatus Status { get; set; }

    public string ModuleIdsJson { get; set; } = "[]";

    public string DatabaseRootPassword { get; set; } = string.Empty;

    public int DatabasePort { get; set; }

    public int AuthServerPort { get; set; }

    public int WorldServerPort { get; set; }

    public int SoapPort { get; set; }

    public int MaxPlayers { get; set; }

    public string RealmName { get; set; } = string.Empty;

    public string CustomEnvVarsJson { get; set; } = "{}";
    
    public string SoapUsername { get; set; } = "admin";
    
    public string SoapPassword { get; set; } = "admin";

    public DateTime CreatedAt { get; set; }
    
    // ===== Version Tracking (captured at build time) =====
    public string CoreRepositoryUrl { get; set; } = string.Empty;
    
    public string CoreBranch { get; set; } = string.Empty;
    
    public string CoreCommitSha { get; set; } = string.Empty;
    
    public DateTime? LastBuiltAt { get; set; }
    
    public string ModuleVersionsJson { get; set; } = "[]";
    
    // ===== Update Status (cached by background service) =====
    public bool IsOutdated { get; set; }
    
    public bool IsCoreOutdated { get; set; }
    
    public int OutdatedModuleCount { get; set; }
    
    public string? LatestAvailableCoreSha { get; set; }
    
    public string OutdatedModulesJson { get; set; } = "[]";
    
    public DateTime? LastUpdateCheckAt { get; set; }
    
    // ===== CI/CD Build Status (cached with update checks) =====
    /// <summary>
    /// CI build status for the latest available core version: "success", "failure", "pending", "unknown"
    /// </summary>
    public string? LatestCoreBuildStatus { get; set; }
    
    /// <summary>
    /// JSON array of critical CI check results for latest core version
    /// </summary>
    public string? LatestCoreBuildChecksJson { get; set; }
    
    /// <summary>
    /// When the CI build status was last checked
    /// </summary>
    public DateTime? LatestCoreBuildStatusCheckedAt { get; set; }
}

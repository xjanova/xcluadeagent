namespace XcluadeAgent.Core.Interfaces;

/// <summary>
/// Service for interacting with GitHub API
/// </summary>
public interface IGitHubService
{
    /// <summary>
    /// Get latest release for a repository
    /// </summary>
    Task<GitHubRelease?> GetLatestReleaseAsync(string repository, string? tagPattern = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all releases for a repository
    /// </summary>
    Task<IEnumerable<GitHubRelease>> GetReleasesAsync(string repository, int count = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Download release assets
    /// </summary>
    Task<string> DownloadReleaseAsync(string repository, string tagName, string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get repository info
    /// </summary>
    Task<GitHubRepository?> GetRepositoryAsync(string repository, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate webhook signature
    /// </summary>
    bool ValidateWebhookSignature(string payload, string signature, string secret);

    /// <summary>
    /// Get rate limit status
    /// </summary>
    Task<RateLimitInfo> GetRateLimitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Test connection and authentication
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// GitHub release info
/// </summary>
public class GitHubRelease
{
    public long Id { get; set; }
    public string TagName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Body { get; set; }
    public bool IsDraft { get; set; }
    public bool IsPrerelease { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime PublishedAt { get; set; }
    public string? ZipballUrl { get; set; }
    public string? TarballUrl { get; set; }
    public List<GitHubAsset> Assets { get; set; } = [];
}

/// <summary>
/// GitHub release asset
/// </summary>
public class GitHubAsset
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string BrowserDownloadUrl { get; set; } = string.Empty;
    public int DownloadCount { get; set; }
}

/// <summary>
/// GitHub repository info
/// </summary>
public class GitHubRepository
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPrivate { get; set; }
    public string DefaultBranch { get; set; } = "main";
    public string? Language { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime PushedAt { get; set; }
}

/// <summary>
/// Rate limit info
/// </summary>
public class RateLimitInfo
{
    public int Limit { get; set; }
    public int Remaining { get; set; }
    public DateTime ResetAt { get; set; }
}

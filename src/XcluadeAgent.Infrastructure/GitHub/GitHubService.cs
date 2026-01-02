using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Infrastructure.GitHub;

/// <summary>
/// GitHub API service implementation
/// </summary>
public class GitHubService : IGitHubService
{
    private readonly GitHubClient _client;
    private readonly GitHubConfig _config;
    private readonly ILogger<GitHubService> _logger;
    private readonly HttpClient _httpClient;

    public GitHubService(
        IOptions<GitHubConfig> config,
        ILogger<GitHubService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _config = config.Value;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("GitHub");

        _client = new GitHubClient(new ProductHeaderValue("XcluadeAgent", "1.0.0"));

        if (!string.IsNullOrEmpty(_config.AccessToken))
        {
            _client.Credentials = new Credentials(_config.AccessToken);
        }

        if (!string.IsNullOrEmpty(_config.ApiBaseUrl) && _config.ApiBaseUrl != "https://api.github.com")
        {
            _client = new GitHubClient(new ProductHeaderValue("XcluadeAgent", "1.0.0"),
                new Uri(_config.ApiBaseUrl));

            if (!string.IsNullOrEmpty(_config.AccessToken))
            {
                _client.Credentials = new Credentials(_config.AccessToken);
            }
        }
    }

    public async Task<GitHubRelease?> GetLatestReleaseAsync(string repository, string? tagPattern = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var (owner, repo) = ParseRepository(repository);

            if (string.IsNullOrEmpty(tagPattern))
            {
                var release = await _client.Repository.Release.GetLatest(owner, repo);
                return MapRelease(release);
            }

            // If pattern specified, get all releases and filter
            var releases = await _client.Repository.Release.GetAll(owner, repo, new ApiOptions { PageCount = 1, PageSize = 50 });
            var regex = new Regex(WildcardToRegex(tagPattern), RegexOptions.IgnoreCase);

            var matchingRelease = releases
                .Where(r => !r.Draft && regex.IsMatch(r.TagName))
                .OrderByDescending(r => r.PublishedAt)
                .FirstOrDefault();

            return matchingRelease != null ? MapRelease(matchingRelease) : null;
        }
        catch (NotFoundException)
        {
            _logger.LogWarning("No releases found for repository {Repository}", repository);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest release for {Repository}", repository);
            throw;
        }
    }

    public async Task<IEnumerable<GitHubRelease>> GetReleasesAsync(string repository, int count = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            var (owner, repo) = ParseRepository(repository);
            var releases = await _client.Repository.Release.GetAll(owner, repo, new ApiOptions { PageCount = 1, PageSize = count });

            return releases
                .Where(r => !r.Draft)
                .Select(MapRelease)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get releases for {Repository}", repository);
            throw;
        }
    }

    public async Task<string> DownloadReleaseAsync(string repository, string tagName, string destinationPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var (owner, repo) = ParseRepository(repository);
            var release = await _client.Repository.Release.Get(owner, repo, tagName);

            // Create temp directory for download
            var tempDir = Path.Combine(Path.GetTempPath(), $"xcluade_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            string downloadPath;

            // Check for zip asset first
            var zipAsset = release.Assets.FirstOrDefault(a =>
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            if (zipAsset != null)
            {
                downloadPath = Path.Combine(tempDir, zipAsset.Name);
                await DownloadFileAsync(zipAsset.BrowserDownloadUrl, downloadPath, cancellationToken);
            }
            else
            {
                // Download zipball (source code)
                downloadPath = Path.Combine(tempDir, $"{repo}-{tagName}.zip");
                var zipballUrl = release.ZipballUrl;

                // Add authentication header for private repos
                using var request = new HttpRequestMessage(HttpMethod.Get, zipballUrl);
                if (!string.IsNullOrEmpty(_config.AccessToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.AccessToken);
                }
                request.Headers.UserAgent.ParseAdd("XcluadeAgent/1.0.0");

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var fileStream = File.Create(downloadPath);
                await response.Content.CopyToAsync(fileStream, cancellationToken);
            }

            _logger.LogInformation("Downloaded release {Tag} to {Path}", tagName, downloadPath);
            return downloadPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download release {Tag} for {Repository}", tagName, repository);
            throw;
        }
    }

    public async Task<GitHubRepository?> GetRepositoryAsync(string repository, CancellationToken cancellationToken = default)
    {
        try
        {
            var (owner, repo) = ParseRepository(repository);
            var repoData = await _client.Repository.Get(owner, repo);

            return new GitHubRepository
            {
                Id = repoData.Id,
                Name = repoData.Name,
                FullName = repoData.FullName,
                Description = repoData.Description,
                IsPrivate = repoData.Private,
                DefaultBranch = repoData.DefaultBranch,
                Language = repoData.Language,
                UpdatedAt = repoData.UpdatedAt.UtcDateTime,
                PushedAt = repoData.PushedAt?.UtcDateTime ?? DateTime.MinValue
            };
        }
        catch (NotFoundException)
        {
            _logger.LogWarning("Repository not found: {Repository}", repository);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get repository info for {Repository}", repository);
            throw;
        }
    }

    public bool ValidateWebhookSignature(string payload, string signature, string secret)
    {
        if (string.IsNullOrEmpty(signature) || !signature.StartsWith("sha256="))
            return false;

        var expectedSignature = signature[7..]; // Remove "sha256=" prefix

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var actualSignature = Convert.ToHexString(hash).ToLower();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature.ToLower()),
            Encoding.UTF8.GetBytes(actualSignature));
    }

    public async Task<RateLimitInfo> GetRateLimitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var rateLimit = await _client.RateLimit.GetRateLimits();
            var core = rateLimit.Resources.Core;

            return new RateLimitInfo
            {
                Limit = core.Limit,
                Remaining = core.Remaining,
                ResetAt = core.Reset.UtcDateTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get rate limit");
            throw;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _client.User.Current();
            _logger.LogInformation("GitHub connection successful. Authenticated as: {Username}", user.Login);
            return true;
        }
        catch (AuthorizationException)
        {
            _logger.LogWarning("GitHub authentication failed. Token may be invalid.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GitHub connection test failed");
            return false;
        }
    }

    private static (string owner, string repo) ParseRepository(string repository)
    {
        var parts = repository.Split('/');
        if (parts.Length != 2)
            throw new ArgumentException($"Invalid repository format: {repository}. Expected format: owner/repo");

        return (parts[0], parts[1]);
    }

    private static Core.Interfaces.GitHubRelease MapRelease(Release release) => new()
    {
        Id = release.Id,
        TagName = release.TagName,
        Name = release.Name,
        Body = release.Body,
        IsDraft = release.Draft,
        IsPrerelease = release.Prerelease,
        CreatedAt = release.CreatedAt.UtcDateTime,
        PublishedAt = release.PublishedAt?.UtcDateTime ?? release.CreatedAt.UtcDateTime,
        ZipballUrl = release.ZipballUrl,
        TarballUrl = release.TarballUrl,
        Assets = release.Assets.Select(a => new Core.Interfaces.GitHubAsset
        {
            Id = a.Id,
            Name = a.Name,
            ContentType = a.ContentType,
            Size = a.Size,
            BrowserDownloadUrl = a.BrowserDownloadUrl,
            DownloadCount = a.DownloadCount
        }).ToList()
    };

    private async Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(_config.AccessToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.AccessToken);
        }
        request.Headers.UserAgent.ParseAdd("XcluadeAgent/1.0.0");
        request.Headers.Accept.ParseAdd("application/octet-stream");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var fileStream = File.Create(destinationPath);
        await response.Content.CopyToAsync(fileStream, cancellationToken);
    }

    private static string WildcardToRegex(string pattern)
    {
        return "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
    }
}

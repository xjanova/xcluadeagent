using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Infrastructure.GitHub;

/// <summary>
/// GitHub OAuth service implementation
/// </summary>
public class GitHubOAuthService : IGitHubOAuthService
{
    private readonly GitHubConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubOAuthService> _logger;

    private const string GitHubAuthorizeUrl = "https://github.com/login/oauth/authorize";
    private const string GitHubTokenUrl = "https://github.com/login/oauth/access_token";
    private const string GitHubApiUrl = "https://api.github.com";

    public GitHubOAuthService(
        IOptions<GitHubConfig> config,
        IHttpClientFactory httpClientFactory,
        ILogger<GitHubOAuthService> logger)
    {
        _config = config.Value;
        _httpClient = httpClientFactory.CreateClient("GitHub");
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsConfigured => _config.OAuth.IsConfigured;

    /// <inheritdoc/>
    public string GetAuthorizationUrl(string state, string redirectUri)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("GitHub OAuth is not configured. Please set ClientId and ClientSecret.");
        }

        var scopes = Uri.EscapeDataString(_config.OAuth.Scopes);
        var encodedRedirectUri = Uri.EscapeDataString(redirectUri);
        var encodedState = Uri.EscapeDataString(state);

        var url = $"{GitHubAuthorizeUrl}?" +
                  $"client_id={_config.OAuth.ClientId}" +
                  $"&redirect_uri={encodedRedirectUri}" +
                  $"&scope={scopes}" +
                  $"&state={encodedState}" +
                  $"&allow_signup=false";

        _logger.LogDebug("Generated GitHub OAuth URL: {Url}", url);
        return url;
    }

    /// <inheritdoc/>
    public async Task<GitHubOAuthTokenResponse?> ExchangeCodeAsync(string code, string redirectUri)
    {
        if (!IsConfigured)
        {
            _logger.LogError("GitHub OAuth is not configured");
            return new GitHubOAuthTokenResponse
            {
                Error = "not_configured",
                ErrorDescription = "GitHub OAuth is not configured"
            };
        }

        try
        {
            var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _config.OAuth.ClientId!,
                ["client_secret"] = _config.OAuth.ClientSecret!,
                ["code"] = code,
                ["redirect_uri"] = redirectUri
            });

            var request = new HttpRequestMessage(HttpMethod.Post, GitHubTokenUrl)
            {
                Content = requestContent
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("GitHub token exchange response: {Content}", content);

            var tokenResponse = JsonSerializer.Deserialize<GitHubTokenResponseInternal>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse == null)
            {
                return new GitHubOAuthTokenResponse
                {
                    Error = "parse_error",
                    ErrorDescription = "Failed to parse GitHub response"
                };
            }

            return new GitHubOAuthTokenResponse
            {
                AccessToken = tokenResponse.AccessToken ?? string.Empty,
                TokenType = tokenResponse.TokenType ?? "bearer",
                Scope = tokenResponse.Scope ?? string.Empty,
                Error = tokenResponse.Error,
                ErrorDescription = tokenResponse.ErrorDescription
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to exchange GitHub OAuth code");
            return new GitHubOAuthTokenResponse
            {
                Error = "exchange_failed",
                ErrorDescription = ex.Message
            };
        }
    }

    /// <inheritdoc/>
    public async Task<GitHubUserInfo?> GetUserInfoAsync(string accessToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{GitHubApiUrl}/user");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub API returned {StatusCode} when fetching user info", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var userInfo = JsonSerializer.Deserialize<GitHubUserResponseInternal>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (userInfo == null)
            {
                return null;
            }

            // Try to get email if not public
            string? email = userInfo.Email;
            if (string.IsNullOrEmpty(email))
            {
                email = await GetPrimaryEmailAsync(accessToken);
            }

            return new GitHubUserInfo
            {
                Id = userInfo.Id.ToString(),
                Login = userInfo.Login ?? string.Empty,
                Name = userInfo.Name,
                Email = email,
                AvatarUrl = userInfo.AvatarUrl
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get GitHub user info");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateTokenAsync(string accessToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{GitHubApiUrl}/user");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate GitHub token");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task RevokeTokenAsync(string accessToken)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Cannot revoke token - OAuth not configured");
            return;
        }

        try
        {
            // GitHub OAuth app token revocation
            var request = new HttpRequestMessage(HttpMethod.Delete,
                $"{GitHubApiUrl}/applications/{_config.OAuth.ClientId}/token");

            var credentials = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{_config.OAuth.ClientId}:{_config.OAuth.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Content = JsonContent.Create(new { access_token = accessToken });

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully revoked GitHub access token");
            }
            else
            {
                _logger.LogWarning("Failed to revoke GitHub token: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke GitHub token");
        }
    }

    private async Task<string?> GetPrimaryEmailAsync(string accessToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{GitHubApiUrl}/user/emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var emails = JsonSerializer.Deserialize<List<GitHubEmailInternal>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return emails?.FirstOrDefault(e => e.Primary)?.Email
                   ?? emails?.FirstOrDefault(e => e.Verified)?.Email;
        }
        catch
        {
            return null;
        }
    }

    #region Internal DTOs for JSON deserialization

    private class GitHubTokenResponseInternal
    {
        public string? AccessToken { get; set; }
        public string? TokenType { get; set; }
        public string? Scope { get; set; }
        public string? Error { get; set; }
        public string? ErrorDescription { get; set; }
    }

    private class GitHubUserResponseInternal
    {
        public long Id { get; set; }
        public string? Login { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? AvatarUrl { get; set; }
    }

    private class GitHubEmailInternal
    {
        public string Email { get; set; } = string.Empty;
        public bool Primary { get; set; }
        public bool Verified { get; set; }
    }

    #endregion
}

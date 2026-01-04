namespace XcluadeAgent.Core.Interfaces;

/// <summary>
/// Service for GitHub OAuth authentication
/// </summary>
public interface IGitHubOAuthService
{
    /// <summary>
    /// Generate OAuth authorization URL for GitHub
    /// </summary>
    /// <param name="state">CSRF protection state parameter</param>
    /// <param name="redirectUri">OAuth callback URI</param>
    /// <returns>Authorization URL to redirect user to</returns>
    string GetAuthorizationUrl(string state, string redirectUri);

    /// <summary>
    /// Exchange authorization code for access token
    /// </summary>
    /// <param name="code">Authorization code from GitHub callback</param>
    /// <param name="redirectUri">OAuth callback URI (must match authorization request)</param>
    /// <returns>OAuth token response</returns>
    Task<GitHubOAuthTokenResponse?> ExchangeCodeAsync(string code, string redirectUri);

    /// <summary>
    /// Get authenticated user information from GitHub
    /// </summary>
    /// <param name="accessToken">GitHub access token</param>
    /// <returns>GitHub user information</returns>
    Task<GitHubUserInfo?> GetUserInfoAsync(string accessToken);

    /// <summary>
    /// Validate an existing access token
    /// </summary>
    /// <param name="accessToken">GitHub access token to validate</param>
    /// <returns>True if token is valid</returns>
    Task<bool> ValidateTokenAsync(string accessToken);

    /// <summary>
    /// Revoke an access token
    /// </summary>
    /// <param name="accessToken">Token to revoke</param>
    Task RevokeTokenAsync(string accessToken);

    /// <summary>
    /// Check if OAuth is configured
    /// </summary>
    bool IsConfigured { get; }
}

/// <summary>
/// GitHub OAuth token response
/// </summary>
public class GitHubOAuthTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string? ErrorDescription { get; set; }
}

/// <summary>
/// GitHub user information
/// </summary>
public class GitHubUserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;
using XcluadeAgent.Infrastructure.Security;
using XcluadeAgent.Shared.DTOs;
using CustomClaimTypes = XcluadeAgent.Shared.Constants.ClaimTypes;
using AppConstants = XcluadeAgent.Shared.Constants.AppConstants;

namespace XcluadeAgent.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ITwoFactorService _twoFactorService;
    private readonly IGitHubOAuthService _gitHubOAuthService;
    private readonly ITokenEncryptionService _tokenEncryption;
    private readonly IMemoryCache _cache;
    private readonly SecurityConfig _securityConfig;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserRepository userRepository,
        IAuditLogRepository auditLogRepository,
        ITwoFactorService twoFactorService,
        IGitHubOAuthService gitHubOAuthService,
        ITokenEncryptionService tokenEncryption,
        IMemoryCache cache,
        IOptions<SecurityConfig> securityConfig,
        ILogger<AuthController> logger)
    {
        _userRepository = userRepository;
        _auditLogRepository = auditLogRepository;
        _twoFactorService = twoFactorService;
        _gitHubOAuthService = gitHubOAuthService;
        _tokenEncryption = tokenEncryption;
        _cache = cache;
        _securityConfig = securityConfig.Value;
        _logger = logger;
    }

    /// <summary>
    /// Login and get JWT token
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await _userRepository.GetByUsernameAsync(request.Username);

        if (user == null)
        {
            return Unauthorized(new LoginResponse
            {
                Success = false,
                ErrorMessage = "Invalid username or password"
            });
        }

        // Check if locked
        if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
        {
            return Unauthorized(new LoginResponse
            {
                Success = false,
                ErrorMessage = $"Account is locked. Try again after {user.LockedUntil:HH:mm:ss}"
            });
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;

            if (user.FailedLoginAttempts >= _securityConfig.MaxFailedLogins)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(_securityConfig.LockoutMinutes);
                _logger.LogWarning("Account locked for user {Username}", user.Username);
            }

            await _userRepository.UpdateAsync(user);

            return Unauthorized(new LoginResponse
            {
                Success = false,
                ErrorMessage = "Invalid username or password"
            });
        }

        // Check if active
        if (!user.IsActive)
        {
            return Unauthorized(new LoginResponse
            {
                Success = false,
                ErrorMessage = "Account is disabled"
            });
        }

        // Check 2FA if enabled
        if (user.TwoFactorEnabled)
        {
            if (string.IsNullOrEmpty(request.TwoFactorCode))
            {
                return Ok(new LoginResponse
                {
                    Success = false,
                    RequiresTwoFactor = true,
                    ErrorMessage = "Two-factor authentication required"
                });
            }

            // Verify 2FA code
            if (string.IsNullOrEmpty(user.TwoFactorSecret) ||
                !_twoFactorService.ValidateCode(user.TwoFactorSecret, request.TwoFactorCode))
            {
                _logger.LogWarning("Invalid 2FA code for user {Username}", user.Username);
                return Unauthorized(new LoginResponse
                {
                    Success = false,
                    RequiresTwoFactor = true,
                    ErrorMessage = "Invalid two-factor authentication code"
                });
            }
        }

        // Reset failed attempts and generate tokens
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        user.LastLoginIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        // Generate tokens
        var token = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();

        // Store refresh token (valid for 7 days by default, or 30 days if remember me)
        var refreshTokenDays = request.RememberMe ? 30 : 7;
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(refreshTokenDays);
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);

        // Log audit
        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = user.Id,
            Username = user.Username,
            Action = "Login",
            IpAddress = user.LastLoginIp,
            UserAgent = Request.Headers.UserAgent
        });

        _logger.LogInformation("User {Username} logged in", user.Username);

        return Ok(new LoginResponse
        {
            Success = true,
            Token = token,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_securityConfig.JwtExpirationMinutes),
            User = MapToDto(user),
            RequirePasswordChange = user.RequirePasswordChange
        });
    }

    /// <summary>
    /// Refresh JWT token
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            // Validate the expired JWT token to get user ID
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_securityConfig.JwtSecret);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = "XcluadeAgent",
                ValidateAudience = true,
                ValidAudience = "XcluadeAgent",
                ValidateLifetime = false, // Allow expired tokens for refresh
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(request.Token, validationParameters, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return BadRequest(new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid token"
                });
            }

            var userIdClaim = principal.FindFirst(CustomClaimTypes.UserId)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return BadRequest(new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid token claims"
                });
            }

            // Get user and validate refresh token
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return BadRequest(new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "User not found"
                });
            }

            // Validate refresh token
            if (user.RefreshToken != request.RefreshToken ||
                user.RefreshTokenExpiresAt == null ||
                user.RefreshTokenExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Invalid or expired refresh token for user {Username}", user.Username);
                return BadRequest(new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid or expired refresh token"
                });
            }

            // Check if user is still active
            if (!user.IsActive)
            {
                return Unauthorized(new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "Account is disabled"
                });
            }

            // Generate new tokens
            var newToken = GenerateJwtToken(user);
            var newRefreshToken = GenerateRefreshToken();

            // Update refresh token (rotate for security)
            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7);
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);

            _logger.LogInformation("Token refreshed for user {Username}", user.Username);

            return Ok(new LoginResponse
            {
                Success = true,
                Token = newToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_securityConfig.JwtExpirationMinutes),
                User = MapToDto(user)
            });
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Token validation failed during refresh");
            return BadRequest(new LoginResponse
            {
                Success = false,
                ErrorMessage = "Invalid token"
            });
        }
    }

    /// <summary>
    /// Logout
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse>> Logout()
    {
        var userId = User.FindFirst(CustomClaimTypes.UserId)?.Value;

        if (userId != null && Guid.TryParse(userId, out var id))
        {
            // Invalidate refresh token
            var user = await _userRepository.GetByIdAsync(id);
            if (user != null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiresAt = null;
                user.UpdatedAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);
            }

            await _auditLogRepository.CreateAsync(new AuditLog
            {
                UserId = id,
                Username = User.Identity?.Name,
                Action = "Logout",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
        }

        return Ok(ApiResponse.Ok("Logged out successfully"));
    }

    /// <summary>
    /// Get current user info
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetCurrentUser()
    {
        var userId = User.FindFirst(CustomClaimTypes.UserId)?.Value;

        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(ApiResponse<UserDto>.Ok(MapToDto(user)));
    }

    /// <summary>
    /// Change password
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<ApiResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmPassword)
        {
            return BadRequest(ApiResponse.Fail("Passwords do not match"));
        }

        if (request.NewPassword.Length < AppConstants.Security.MinPasswordLength)
        {
            return BadRequest(ApiResponse.Fail($"Password must be at least {AppConstants.Security.MinPasswordLength} characters"));
        }

        var userId = User.FindFirst(CustomClaimTypes.UserId)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return BadRequest(ApiResponse.Fail("Current password is incorrect"));
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.RequirePasswordChange = false; // Clear the flag after password change
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = user.Id,
            Username = user.Username,
            Action = "PasswordChanged",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        return Ok(ApiResponse.Ok("Password changed successfully"));
    }

    /// <summary>
    /// Setup 2FA - Generate secret and QR code URI
    /// </summary>
    [HttpPost("2fa/setup")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<TwoFactorSetupResponse>>> Setup2FA()
    {
        var userId = User.FindFirst(CustomClaimTypes.UserId)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        if (user.TwoFactorEnabled)
        {
            return BadRequest(ApiResponse.Fail("Two-factor authentication is already enabled"));
        }

        // Generate new secret
        var secret = _twoFactorService.GenerateSecretKey();
        var qrCodeUri = _twoFactorService.GenerateQrCodeUri(secret, user.Email);

        // Store secret temporarily (will be confirmed in enable endpoint)
        user.TwoFactorSecret = secret;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        return Ok(ApiResponse<TwoFactorSetupResponse>.Ok(new TwoFactorSetupResponse
        {
            Secret = secret,
            QrCodeUri = qrCodeUri
        }));
    }

    /// <summary>
    /// Enable 2FA after verifying code
    /// </summary>
    [HttpPost("2fa/enable")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<TwoFactorEnableResponse>>> Enable2FA([FromBody] Enable2FARequest request)
    {
        var userId = User.FindFirst(CustomClaimTypes.UserId)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        if (user.TwoFactorEnabled)
        {
            return BadRequest(ApiResponse.Fail("Two-factor authentication is already enabled"));
        }

        if (string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            return BadRequest(ApiResponse.Fail("Please setup 2FA first using /2fa/setup endpoint"));
        }

        // Verify the code
        if (!_twoFactorService.ValidateCode(user.TwoFactorSecret, request.Code))
        {
            return BadRequest(ApiResponse.Fail("Invalid verification code"));
        }

        // Enable 2FA and generate backup codes
        var backupCodes = _twoFactorService.GenerateBackupCodes();
        user.TwoFactorEnabled = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = user.Id,
            Username = user.Username,
            Action = "TwoFactorEnabled",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        _logger.LogInformation("2FA enabled for user {Username}", user.Username);

        return Ok(ApiResponse<TwoFactorEnableResponse>.Ok(new TwoFactorEnableResponse
        {
            Enabled = true,
            BackupCodes = backupCodes
        }));
    }

    /// <summary>
    /// Disable 2FA
    /// </summary>
    [HttpPost("2fa/disable")]
    [Authorize]
    public async Task<ActionResult<ApiResponse>> Disable2FA([FromBody] Disable2FARequest request)
    {
        var userId = User.FindFirst(CustomClaimTypes.UserId)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        if (!user.TwoFactorEnabled)
        {
            return BadRequest(ApiResponse.Fail("Two-factor authentication is not enabled"));
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return BadRequest(ApiResponse.Fail("Invalid password"));
        }

        // Disable 2FA
        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = user.Id,
            Username = user.Username,
            Action = "TwoFactorDisabled",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        _logger.LogInformation("2FA disabled for user {Username}", user.Username);

        return Ok(ApiResponse.Ok("Two-factor authentication disabled"));
    }

    #region GitHub OAuth - One Click Setup

    /// <summary>
    /// Get GitHub OAuth status for current user
    /// </summary>
    [HttpGet("github/status")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<GitHubOAuthStatusResponse>>> GetGitHubStatus()
    {
        var userId = User.FindFirst(CustomClaimTypes.UserId)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(ApiResponse<GitHubOAuthStatusResponse>.Ok(new GitHubOAuthStatusResponse
        {
            IsLinked = !string.IsNullOrEmpty(user.GitHubId),
            GitHubUsername = user.GitHubUsername,
            GitHubId = user.GitHubId,
            LinkedAt = user.GitHubLinkedAt,
            TokenScopes = user.GitHubTokenScopes,
            IsOAuthConfigured = _gitHubOAuthService.IsConfigured
        }));
    }

    /// <summary>
    /// Get GitHub OAuth authorization URL for one-click setup
    /// </summary>
    [HttpGet("github/authorize")]
    [Authorize]
    [EnableRateLimiting("auth")]
    public ActionResult<ApiResponse<GitHubOAuthUrlResponse>> GetGitHubAuthUrl()
    {
        if (!_gitHubOAuthService.IsConfigured)
        {
            return BadRequest(ApiResponse.Fail("GitHub OAuth is not configured. Please configure Client ID and Secret in settings."));
        }

        var userId = User.FindFirst(CustomClaimTypes.UserId)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        // Generate CSRF state token
        var state = GenerateSecureToken();

        // Store state in cache with user ID (expires in 10 minutes)
        _cache.Set($"github_oauth_state_{state}", userId, TimeSpan.FromMinutes(10));

        // Build redirect URI
        var redirectUri = $"{Request.Scheme}://{Request.Host}/api/v1/auth/github/callback";

        var authUrl = _gitHubOAuthService.GetAuthorizationUrl(state, redirectUri);

        return Ok(ApiResponse<GitHubOAuthUrlResponse>.Ok(new GitHubOAuthUrlResponse
        {
            AuthorizationUrl = authUrl,
            State = state
        }));
    }

    /// <summary>
    /// Handle GitHub OAuth callback - exchanges code for token and links account
    /// </summary>
    [HttpGet("github/callback")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> GitHubCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        // Handle OAuth errors
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("GitHub OAuth error: {Error}", error);
            return Redirect($"/?github_error={Uri.EscapeDataString(error)}");
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            return Redirect("/?github_error=missing_parameters");
        }

        // Validate state and get user ID
        if (!_cache.TryGetValue($"github_oauth_state_{state}", out string? userId))
        {
            _logger.LogWarning("Invalid or expired OAuth state: {State}", state);
            return Redirect("/?github_error=invalid_state");
        }

        // Remove state from cache (one-time use)
        _cache.Remove($"github_oauth_state_{state}");

        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Redirect("/?github_error=invalid_user");
        }

        // Exchange code for token
        var redirectUri = $"{Request.Scheme}://{Request.Host}/api/v1/auth/github/callback";
        var tokenResponse = await _gitHubOAuthService.ExchangeCodeAsync(code, redirectUri);

        if (tokenResponse == null || !string.IsNullOrEmpty(tokenResponse.Error))
        {
            var errorMsg = tokenResponse?.ErrorDescription ?? tokenResponse?.Error ?? "token_exchange_failed";
            _logger.LogError("GitHub token exchange failed: {Error}", errorMsg);
            return Redirect($"/?github_error={Uri.EscapeDataString(errorMsg)}");
        }

        // Get user info from GitHub
        var gitHubUser = await _gitHubOAuthService.GetUserInfoAsync(tokenResponse.AccessToken);
        if (gitHubUser == null)
        {
            _logger.LogError("Failed to get GitHub user info");
            return Redirect("/?github_error=user_info_failed");
        }

        // Update user with GitHub info
        var user = await _userRepository.GetByIdAsync(userGuid);
        if (user == null)
        {
            return Redirect("/?github_error=user_not_found");
        }

        user.GitHubId = gitHubUser.Id;
        user.GitHubUsername = gitHubUser.Login;
        user.GitHubAccessToken = _tokenEncryption.Encrypt(tokenResponse.AccessToken);
        user.GitHubTokenScopes = tokenResponse.Scope;
        user.GitHubLinkedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);

        // Audit log
        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = user.Id,
            Username = user.Username,
            Action = "GitHubLinked",
            NewValue = $"GitHub: {gitHubUser.Login} ({gitHubUser.Id})",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        _logger.LogInformation("User {Username} linked GitHub account: {GitHubUser}", user.Username, gitHubUser.Login);

        // Redirect back to dashboard with success message
        return Redirect($"/?github_success=true&github_user={Uri.EscapeDataString(gitHubUser.Login)}");
    }

    /// <summary>
    /// Handle GitHub OAuth callback via POST (API method for SPA)
    /// </summary>
    [HttpPost("github/callback")]
    [Authorize]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<GitHubOAuthLinkResponse>>> GitHubCallbackPost([FromBody] GitHubOAuthCallbackRequest request)
    {
        var userId = User.FindFirst(CustomClaimTypes.UserId)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Unauthorized();
        }

        // Validate state
        if (!_cache.TryGetValue($"github_oauth_state_{request.State}", out string? cachedUserId) ||
            cachedUserId != userId)
        {
            return BadRequest(ApiResponse<GitHubOAuthLinkResponse>.Fail("Invalid or expired state token"));
        }

        // Remove state from cache
        _cache.Remove($"github_oauth_state_{request.State}");

        // Exchange code for token
        var redirectUri = $"{Request.Scheme}://{Request.Host}/api/v1/auth/github/callback";
        var tokenResponse = await _gitHubOAuthService.ExchangeCodeAsync(request.Code, redirectUri);

        if (tokenResponse == null || !string.IsNullOrEmpty(tokenResponse.Error))
        {
            return BadRequest(ApiResponse<GitHubOAuthLinkResponse>.Fail(
                tokenResponse?.ErrorDescription ?? "Failed to exchange authorization code"));
        }

        // Get user info
        var gitHubUser = await _gitHubOAuthService.GetUserInfoAsync(tokenResponse.AccessToken);
        if (gitHubUser == null)
        {
            return BadRequest(ApiResponse<GitHubOAuthLinkResponse>.Fail("Failed to get GitHub user information"));
        }

        // Update user
        var user = await _userRepository.GetByIdAsync(userGuid);
        if (user == null)
        {
            return NotFound();
        }

        user.GitHubId = gitHubUser.Id;
        user.GitHubUsername = gitHubUser.Login;
        user.GitHubAccessToken = _tokenEncryption.Encrypt(tokenResponse.AccessToken);
        user.GitHubTokenScopes = tokenResponse.Scope;
        user.GitHubLinkedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);

        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = user.Id,
            Username = user.Username,
            Action = "GitHubLinked",
            NewValue = $"GitHub: {gitHubUser.Login} ({gitHubUser.Id})",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        _logger.LogInformation("User {Username} linked GitHub account via API: {GitHubUser}", user.Username, gitHubUser.Login);

        return Ok(ApiResponse<GitHubOAuthLinkResponse>.Ok(new GitHubOAuthLinkResponse
        {
            Success = true,
            Message = "GitHub account linked successfully",
            GitHubUsername = gitHubUser.Login,
            GitHubId = gitHubUser.Id,
            Scopes = tokenResponse.Scope
        }));
    }

    /// <summary>
    /// Unlink GitHub account from current user
    /// </summary>
    [HttpPost("github/unlink")]
    [Authorize]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse>> UnlinkGitHub()
    {
        var userId = User.FindFirst(CustomClaimTypes.UserId)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(user.GitHubId))
        {
            return BadRequest(ApiResponse.Fail("GitHub account is not linked"));
        }

        var oldGitHubUsername = user.GitHubUsername;

        // Revoke token if possible (decrypt first)
        if (!string.IsNullOrEmpty(user.GitHubAccessToken))
        {
            var decryptedToken = _tokenEncryption.Decrypt(user.GitHubAccessToken);
            if (!string.IsNullOrEmpty(decryptedToken))
            {
                await _gitHubOAuthService.RevokeTokenAsync(decryptedToken);
            }
        }

        // Clear GitHub data
        user.GitHubId = null;
        user.GitHubUsername = null;
        user.GitHubAccessToken = null;
        user.GitHubTokenScopes = null;
        user.GitHubLinkedAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);

        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = user.Id,
            Username = user.Username,
            Action = "GitHubUnlinked",
            OldValue = $"GitHub: {oldGitHubUsername}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        _logger.LogInformation("User {Username} unlinked GitHub account: {GitHubUser}", user.Username, oldGitHubUsername);

        return Ok(ApiResponse.Ok("GitHub account unlinked successfully"));
    }

    #endregion

    #region Private Methods

    private string GenerateJwtToken(User user)
    {
        var claims = new[]
        {
            new Claim(CustomClaimTypes.UserId, user.Id.ToString()),
            new Claim(CustomClaimTypes.Username, user.Username),
            new Claim(CustomClaimTypes.Email, user.Email),
            new Claim(CustomClaimTypes.Role, user.Role.ToString()),
            new Claim(CustomClaimTypes.DisplayName, user.DisplayName ?? user.Username)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_securityConfig.JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "XcluadeAgent",
            audience: "XcluadeAgent",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_securityConfig.JwtExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToHexString(randomBytes).ToLowerInvariant();
    }

    private static UserDto MapToDto(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        DisplayName = user.DisplayName,
        Role = user.Role.ToString(),
        IsActive = user.IsActive,
        LastLoginAt = user.LastLoginAt,
        TwoFactorEnabled = user.TwoFactorEnabled,
        GitHubLinked = !string.IsNullOrEmpty(user.GitHubId),
        GitHubUsername = user.GitHubUsername,
        CreatedAt = user.CreatedAt
    };

    #endregion
}

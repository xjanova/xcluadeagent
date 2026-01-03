using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly SecurityConfig _securityConfig;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserRepository userRepository,
        IAuditLogRepository auditLogRepository,
        ITwoFactorService twoFactorService,
        IOptions<SecurityConfig> securityConfig,
        ILogger<AuthController> logger)
    {
        _userRepository = userRepository;
        _auditLogRepository = auditLogRepository;
        _twoFactorService = twoFactorService;
        _securityConfig = securityConfig.Value;
        _logger = logger;
    }

    /// <summary>
    /// Login and get JWT token
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
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
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
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

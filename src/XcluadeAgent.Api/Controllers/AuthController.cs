using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;
using XcluadeAgent.Shared.DTOs;
using CustomClaimTypes = XcluadeAgent.Shared.Constants.ClaimTypes;

namespace XcluadeAgent.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly SecurityConfig _securityConfig;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserRepository userRepository,
        IAuditLogRepository auditLogRepository,
        IOptions<SecurityConfig> securityConfig,
        ILogger<AuthController> logger)
    {
        _userRepository = userRepository;
        _auditLogRepository = auditLogRepository;
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

            // TODO: Verify 2FA code
        }

        // Reset failed attempts
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        user.LastLoginIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _userRepository.UpdateAsync(user);

        // Generate tokens
        var token = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();

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
            User = MapToDto(user)
        });
    }

    /// <summary>
    /// Refresh JWT token
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        // TODO: Implement proper refresh token validation
        return BadRequest(new LoginResponse
        {
            Success = false,
            ErrorMessage = "Invalid refresh token"
        });
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

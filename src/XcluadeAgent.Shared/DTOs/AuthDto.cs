using System.ComponentModel.DataAnnotations;
using XcluadeAgent.Core.Enums;

namespace XcluadeAgent.Shared.DTOs;

/// <summary>
/// Login request
/// </summary>
public class LoginRequest
{
    [Required(ErrorMessage = "Username is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters")]
    public string Password { get; set; } = string.Empty;

    [StringLength(10, ErrorMessage = "Two-factor code must be at most 10 characters")]
    public string? TwoFactorCode { get; set; }

    public bool RememberMe { get; set; } = false;
}

/// <summary>
/// Login response
/// </summary>
public class LoginResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public UserDto? User { get; set; }
    public string? ErrorMessage { get; set; }
    public bool RequiresTwoFactor { get; set; }
    public bool RequirePasswordChange { get; set; }
}

/// <summary>
/// User DTO
/// </summary>
public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool GitHubLinked { get; set; }
    public string? GitHubUsername { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Create user request
/// </summary>
public class CreateUserRequest
{
    [Required(ErrorMessage = "Username is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Username can only contain letters, numbers, underscores and hyphens")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(255, ErrorMessage = "Email must not exceed 255 characters")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters")]
    public string Password { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Display name must not exceed 100 characters")]
    public string? DisplayName { get; set; }

    public UserRole Role { get; set; } = UserRole.Viewer;
}

/// <summary>
/// Update user request
/// </summary>
public class UpdateUserRequest
{
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(255, ErrorMessage = "Email must not exceed 255 characters")]
    public string? Email { get; set; }

    [StringLength(100, ErrorMessage = "Display name must not exceed 100 characters")]
    public string? DisplayName { get; set; }

    public UserRole? Role { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// Change password request
/// </summary>
public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Current password is required")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm password is required")]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>
/// Refresh token request
/// </summary>
public class RefreshTokenRequest
{
    [Required(ErrorMessage = "Token is required")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Refresh token is required")]
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// Two-factor authentication setup response
/// </summary>
public class TwoFactorSetupResponse
{
    public string Secret { get; set; } = string.Empty;
    public string QrCodeUri { get; set; } = string.Empty;
}

/// <summary>
/// Enable 2FA request
/// </summary>
public class Enable2FARequest
{
    [Required(ErrorMessage = "Verification code is required")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Verification code must be 6 digits")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Verification code must be 6 digits")]
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// Two-factor authentication enable response
/// </summary>
public class TwoFactorEnableResponse
{
    public bool Enabled { get; set; }
    public List<string> BackupCodes { get; set; } = [];
}

/// <summary>
/// Disable 2FA request
/// </summary>
public class Disable2FARequest
{
    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// GitHub OAuth authorization URL response
/// </summary>
public class GitHubOAuthUrlResponse
{
    public string AuthorizationUrl { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

/// <summary>
/// GitHub OAuth callback request
/// </summary>
public class GitHubOAuthCallbackRequest
{
    [Required(ErrorMessage = "Authorization code is required")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "State is required")]
    public string State { get; set; } = string.Empty;
}

/// <summary>
/// GitHub OAuth status response
/// </summary>
public class GitHubOAuthStatusResponse
{
    public bool IsLinked { get; set; }
    public string? GitHubUsername { get; set; }
    public string? GitHubId { get; set; }
    public DateTime? LinkedAt { get; set; }
    public string? TokenScopes { get; set; }
    public bool IsOAuthConfigured { get; set; }
}

/// <summary>
/// GitHub OAuth link response after successful authorization
/// </summary>
public class GitHubOAuthLinkResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? GitHubUsername { get; set; }
    public string? GitHubId { get; set; }
    public string? Scopes { get; set; }
}

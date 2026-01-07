using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;
using XcluadeAgent.Core.Enums;
using XcluadeAgent.Shared.DTOs;
using XcluadeAgent.Shared.Constants;

namespace XcluadeAgent.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILicenseService _licenseService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserRepository userRepository,
        IAuditLogRepository auditLogRepository,
        ILicenseService licenseService,
        ILogger<UsersController> logger)
    {
        _userRepository = userRepository;
        _auditLogRepository = auditLogRepository;
        _licenseService = licenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<UsersListResponse>>> GetUsers()
    {
        var users = await _userRepository.GetAllAsync();
        var license = await _licenseService.GetCurrentLicenseAsync();

        var userDtos = users.Select(u => new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            DisplayName = u.DisplayName,
            Role = u.Role.ToString(),
            IsActive = u.IsActive,
            TwoFactorEnabled = u.TwoFactorEnabled,
            GitHubLinked = !string.IsNullOrEmpty(u.GitHubId),
            GitHubUsername = u.GitHubUsername,
            LastLoginAt = u.LastLoginAt,
            CreatedAt = u.CreatedAt
        }).ToList();

        return Ok(ApiResponse<UsersListResponse>.Ok(new UsersListResponse
        {
            Users = userDtos,
            TotalCount = userDtos.Count,
            UserLimit = license?.MaxUsers ?? 1
        }));
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetUser(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound(ApiResponse.Fail("User not found"));
        }

        return Ok(ApiResponse<UserDto>.Ok(new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            TwoFactorEnabled = user.TwoFactorEnabled,
            GitHubLinked = !string.IsNullOrEmpty(user.GitHubId),
            GitHubUsername = user.GitHubUsername,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt
        }));
    }

    /// <summary>
    /// Create new user
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserDto>>> CreateUser([FromBody] CreateUserRequest request)
    {
        // Check license limit
        var license = await _licenseService.GetCurrentLicenseAsync();
        var currentCount = await _userRepository.CountAsync();

        if (license != null && currentCount >= license.MaxUsers)
        {
            return BadRequest(ApiResponse.Fail($"User limit reached ({license.MaxUsers}). Please upgrade your license."));
        }

        // Check if username exists
        var existingUser = await _userRepository.GetByUsernameAsync(request.Username);
        if (existingUser != null)
        {
            return BadRequest(ApiResponse.Fail("Username already exists"));
        }

        // Check if email exists
        existingUser = await _userRepository.GetByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return BadRequest(ApiResponse.Fail("Email already exists"));
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            DisplayName = request.DisplayName ?? request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            IsActive = true,
            RequirePasswordChange = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _userRepository.CreateAsync(user);

        // Audit log
        var currentUserId = User.FindFirst(ClaimTypes.UserId)?.Value;
        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = Guid.TryParse(currentUserId, out var uid) ? uid : null,
            Username = User.Identity?.Name,
            Action = "UserCreated",
            EntityType = "User",
            EntityId = user.Id.ToString(),
            NewValue = $"Created user: {user.Username}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        _logger.LogInformation("User {Username} created by {CurrentUser}", user.Username, User.Identity?.Name);

        return Ok(ApiResponse<UserDto>.Ok(new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        }));
    }

    /// <summary>
    /// Update user
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound(ApiResponse.Fail("User not found"));
        }

        // Check if email is being changed and already exists
        if (request.Email != null && request.Email != user.Email)
        {
            var existingUser = await _userRepository.GetByEmailAsync(request.Email);
            if (existingUser != null && existingUser.Id != id)
            {
                return BadRequest(ApiResponse.Fail("Email already exists"));
            }
        }

        // Get the new role if provided
        var newRole = request.Role ?? user.Role;

        // Prevent demoting the last admin
        if (user.Role == UserRole.SuperAdmin && newRole != UserRole.SuperAdmin)
        {
            var admins = (await _userRepository.GetAllAsync())
                .Count(u => u.Role == UserRole.SuperAdmin || u.Role == UserRole.Admin);
            if (admins <= 1)
            {
                return BadRequest(ApiResponse.Fail("Cannot demote the last admin user"));
            }
        }

        // Update only provided fields
        if (request.Email != null) user.Email = request.Email;
        if (request.DisplayName != null) user.DisplayName = request.DisplayName;
        if (request.Role.HasValue) user.Role = request.Role.Value;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);

        // Audit log
        var currentUserId = User.FindFirst(ClaimTypes.UserId)?.Value;
        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = Guid.TryParse(currentUserId, out var uid) ? uid : null,
            Username = User.Identity?.Name,
            Action = "UserUpdated",
            EntityType = "User",
            EntityId = user.Id.ToString(),
            NewValue = $"Updated user: {user.Username}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        _logger.LogInformation("User {Username} updated by {CurrentUser}", user.Username, User.Identity?.Name);

        return Ok(ApiResponse<UserDto>.Ok(new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            TwoFactorEnabled = user.TwoFactorEnabled,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt
        }));
    }

    /// <summary>
    /// Delete user
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteUser(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound(ApiResponse.Fail("User not found"));
        }

        // Prevent deleting the last admin
        if (user.Role == UserRole.SuperAdmin || user.Role == UserRole.Admin)
        {
            var admins = (await _userRepository.GetAllAsync())
                .Count(u => u.Role == UserRole.SuperAdmin || u.Role == UserRole.Admin);
            if (admins <= 1)
            {
                return BadRequest(ApiResponse.Fail("Cannot delete the last admin user"));
            }
        }

        // Prevent self-deletion
        var currentUserId = User.FindFirst(ClaimTypes.UserId)?.Value;
        if (currentUserId == id.ToString())
        {
            return BadRequest(ApiResponse.Fail("Cannot delete your own account"));
        }

        await _userRepository.DeleteAsync(id);

        // Audit log
        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = Guid.TryParse(currentUserId, out var uid) ? uid : null,
            Username = User.Identity?.Name,
            Action = "UserDeleted",
            EntityType = "User",
            EntityId = id.ToString(),
            OldValue = $"Deleted user: {user.Username}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        _logger.LogInformation("User {Username} deleted by {CurrentUser}", user.Username, User.Identity?.Name);

        return Ok(ApiResponse.Ok("User deleted successfully"));
    }

    /// <summary>
    /// Reset user password
    /// </summary>
    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<ApiResponse<ResetPasswordResponse>>> ResetPassword(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound(ApiResponse.Fail("User not found"));
        }

        // Generate temporary password
        var tempPassword = GenerateTemporaryPassword();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);
        user.RequirePasswordChange = true;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);

        // Audit log
        var currentUserId = User.FindFirst(ClaimTypes.UserId)?.Value;
        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = Guid.TryParse(currentUserId, out var uid) ? uid : null,
            Username = User.Identity?.Name,
            Action = "PasswordReset",
            EntityType = "User",
            EntityId = id.ToString(),
            NewValue = $"Password reset for user: {user.Username}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        _logger.LogInformation("Password reset for user {Username} by {CurrentUser}", user.Username, User.Identity?.Name);

        return Ok(ApiResponse<ResetPasswordResponse>.Ok(new ResetPasswordResponse
        {
            TemporaryPassword = tempPassword,
            Message = "Password has been reset. User must change password on next login."
        }));
    }

    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 12).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}

public class UsersListResponse
{
    public List<UserDto> Users { get; set; } = new();
    public int TotalCount { get; set; }
    public int UserLimit { get; set; }
}

public class ResetPasswordResponse
{
    public string TemporaryPassword { get; set; } = "";
    public string Message { get; set; } = "";
}

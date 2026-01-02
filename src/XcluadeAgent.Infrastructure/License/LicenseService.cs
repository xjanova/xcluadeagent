using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Infrastructure.License;

/// <summary>
/// License management service with GitHub-based validation
/// </summary>
public class LicenseService : ILicenseService
{
    private readonly LicenseConfig _config;
    private readonly ILogger<LicenseService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private Core.Models.License? _currentLicense;
    private DateTime _lastValidation;

    private const string LicenseFileName = "license.json";
    private const string PublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA...
-----END PUBLIC KEY-----"; // Replace with actual public key

    public LicenseService(
        IOptions<LicenseConfig> config,
        ILogger<LicenseService> logger,
        IHttpClientFactory httpClientFactory,
        IProjectRepository projectRepository,
        IUserRepository userRepository)
    {
        _config = config.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _projectRepository = projectRepository;
        _userRepository = userRepository;
    }

    public async Task<Core.Models.License?> GetCurrentLicenseAsync(CancellationToken cancellationToken = default)
    {
        if (_currentLicense != null)
            return _currentLicense;

        // Try to load from local file
        var licensePath = Path.Combine("data", LicenseFileName);
        if (File.Exists(licensePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(licensePath, cancellationToken);
                _currentLicense = JsonSerializer.Deserialize<Core.Models.License>(json);

                if (_currentLicense != null)
                {
                    // Validate on load
                    await ValidateLicenseAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load license file");
            }
        }

        // Try from config
        if (_currentLicense == null && !string.IsNullOrEmpty(_config.Key))
        {
            await ActivateLicenseAsync(_config.Key, cancellationToken);
        }

        return _currentLicense;
    }

    public async Task<LicenseValidationResult> ActivateLicenseAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate key format
            if (!IsValidKeyFormat(key))
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Invalid license key format"
                };
            }

            // Try to validate online
            var license = await ValidateKeyOnlineAsync(key, cancellationToken);

            if (license == null)
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "License key not found or invalid"
                };
            }

            // Check expiration
            if (license.ExpiresAt.HasValue && license.ExpiresAt.Value < DateTime.UtcNow)
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    License = license,
                    ErrorMessage = "License has expired"
                };
            }

            // Save license locally
            license.IsValid = true;
            license.LastValidatedAt = DateTime.UtcNow;
            await SaveLicenseAsync(license, cancellationToken);

            _currentLicense = license;
            _lastValidation = DateTime.UtcNow;

            _logger.LogInformation("License activated: {Type} for {LicensedTo}",
                license.Type, license.LicensedTo);

            return new LicenseValidationResult
            {
                IsValid = true,
                License = license
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate license");
            return new LicenseValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Activation failed: {ex.Message}"
            };
        }
    }

    public async Task<LicenseValidationResult> ValidateLicenseAsync(CancellationToken cancellationToken = default)
    {
        var license = await GetCurrentLicenseAsync(cancellationToken);

        if (license == null)
        {
            return new LicenseValidationResult
            {
                IsValid = false,
                ErrorMessage = "No license found"
            };
        }

        var result = new LicenseValidationResult { License = license };
        var warnings = new List<string>();

        // Check expiration
        if (license.ExpiresAt.HasValue)
        {
            if (license.ExpiresAt.Value < DateTime.UtcNow)
            {
                result.IsValid = false;
                result.ErrorMessage = "License has expired";
                return result;
            }

            var daysRemaining = (license.ExpiresAt.Value - DateTime.UtcNow).Days;
            if (daysRemaining <= 30)
            {
                warnings.Add($"License expires in {daysRemaining} days");
            }
        }

        // Online validation if needed
        var hoursSinceValidation = (DateTime.UtcNow - _lastValidation).TotalHours;
        if (hoursSinceValidation >= _config.ValidationIntervalHours)
        {
            var onlineResult = await ValidateKeyOnlineAsync(license.Key, cancellationToken);

            if (onlineResult == null)
            {
                if (_config.AllowOffline)
                {
                    var daysSinceValidation = (DateTime.UtcNow - (license.LastValidatedAt ?? DateTime.UtcNow)).Days;
                    if (daysSinceValidation > _config.OfflineGraceDays)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = "Offline grace period exceeded. Please connect to the internet to validate license.";
                        return result;
                    }
                    warnings.Add("Unable to validate license online. Running in offline mode.");
                }
                else
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Unable to validate license online";
                    return result;
                }
            }
            else
            {
                _lastValidation = DateTime.UtcNow;
                license.LastValidatedAt = DateTime.UtcNow;
                await SaveLicenseAsync(license, cancellationToken);
            }
        }

        result.IsValid = true;
        result.Warnings = warnings;
        return result;
    }

    public async Task<bool> DeactivateLicenseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var licensePath = Path.Combine("data", LicenseFileName);
            if (File.Exists(licensePath))
            {
                File.Delete(licensePath);
            }

            _currentLicense = null;
            _logger.LogInformation("License deactivated");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deactivate license");
            return false;
        }
    }

    public async Task<bool> IsFeatureAvailableAsync(string featureName, CancellationToken cancellationToken = default)
    {
        var license = await GetCurrentLicenseAsync(cancellationToken);

        if (license == null)
        {
            // Check if feature is available in community edition
            var communityFeatures = LicenseFeatures.GetDefault(LicenseType.Community);
            return GetFeatureValue(communityFeatures, featureName);
        }

        return GetFeatureValue(license.Features, featureName);
    }

    public async Task<LimitCheck> CheckLimitsAsync(CancellationToken cancellationToken = default)
    {
        var license = await GetCurrentLicenseAsync(cancellationToken);
        var (maxProjects, maxUsers) = license != null
            ? (license.MaxProjects, license.MaxUsers)
            : LicenseLimits.GetLimits(LicenseType.Community);

        var currentProjects = await _projectRepository.CountAsync(cancellationToken);
        var currentUsers = await _userRepository.CountAsync(cancellationToken);

        var result = new LimitCheck
        {
            CurrentProjects = currentProjects,
            MaxProjects = maxProjects,
            CurrentUsers = currentUsers,
            MaxUsers = maxUsers,
            WithinLimits = currentProjects <= maxProjects && currentUsers <= maxUsers
        };

        if (currentProjects >= maxProjects * 0.8)
        {
            result.LimitWarnings.Add($"Approaching project limit: {currentProjects}/{maxProjects}");
        }

        if (currentUsers >= maxUsers * 0.8)
        {
            result.LimitWarnings.Add($"Approaching user limit: {currentUsers}/{maxUsers}");
        }

        return result;
    }

    public async Task<LicenseInfo> GetLicenseInfoAsync(CancellationToken cancellationToken = default)
    {
        var license = await GetCurrentLicenseAsync(cancellationToken);
        var limits = await CheckLimitsAsync(cancellationToken);

        if (license == null)
        {
            var info = LicenseInfo.Community();
            info.ProjectsUsed = limits.CurrentProjects;
            info.UsersUsed = limits.CurrentUsers;
            return info;
        }

        var daysRemaining = license.ExpiresAt.HasValue
            ? (int)(license.ExpiresAt.Value - DateTime.UtcNow).TotalDays
            : int.MaxValue;

        return new LicenseInfo
        {
            HasLicense = true,
            Type = license.Type,
            TypeName = LicenseTypeNames.GetName(license.Type),
            LicensedTo = license.LicensedTo,
            ExpiresAt = license.ExpiresAt,
            DaysRemaining = daysRemaining,
            IsExpired = daysRemaining < 0,
            IsExpiringSoon = daysRemaining > 0 && daysRemaining <= 30,
            ProjectsUsed = limits.CurrentProjects,
            ProjectsLimit = license.MaxProjects,
            UsersUsed = limits.CurrentUsers,
            UsersLimit = license.MaxUsers,
            Features = license.Features
        };
    }

    public async Task<LicenseValidationResult> RefreshLicenseAsync(CancellationToken cancellationToken = default)
    {
        var license = await GetCurrentLicenseAsync(cancellationToken);

        if (license == null)
        {
            return new LicenseValidationResult
            {
                IsValid = false,
                ErrorMessage = "No license to refresh"
            };
        }

        return await ActivateLicenseAsync(license.Key, cancellationToken);
    }

    #region Private Methods

    private bool IsValidKeyFormat(string key)
    {
        // Expected format: XXXX-XXXX-XXXX-XXXX or similar
        if (string.IsNullOrWhiteSpace(key)) return false;
        return key.Length >= 16;
    }

    private async Task<Core.Models.License?> ValidateKeyOnlineAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("XcluadeAgent/1.0.0");

            // Try to fetch from GitHub repository
            var response = await client.GetAsync(
                $"{_config.ValidationEndpoint}/{key}.json",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            // The content should be base64 encoded and signed
            var licenseData = ParseLicenseContent(content);
            if (licenseData == null) return null;

            // Verify signature
            if (!VerifySignature(licenseData))
            {
                _logger.LogWarning("License signature verification failed");
                return null;
            }

            return licenseData;
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning("Unable to reach license server - offline mode");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "License validation error");
            return null;
        }
    }

    private Core.Models.License? ParseLicenseContent(string content)
    {
        try
        {
            // Decode base64 if needed
            if (IsBase64(content))
            {
                content = Encoding.UTF8.GetString(Convert.FromBase64String(content));
            }

            return JsonSerializer.Deserialize<Core.Models.License>(content);
        }
        catch
        {
            return null;
        }
    }

    private bool VerifySignature(Core.Models.License license)
    {
        // In a real implementation, verify the signature using public key
        // For now, just check if signature exists
        return !string.IsNullOrEmpty(license.Signature);
    }

    private bool IsBase64(string str)
    {
        try
        {
            Convert.FromBase64String(str);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task SaveLicenseAsync(Core.Models.License license, CancellationToken cancellationToken)
    {
        var licensePath = Path.Combine("data", LicenseFileName);
        Directory.CreateDirectory("data");

        var json = JsonSerializer.Serialize(license, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(licensePath, json, cancellationToken);
    }

    private bool GetFeatureValue(LicenseFeatures features, string featureName)
    {
        var property = typeof(LicenseFeatures).GetProperty(featureName);
        if (property == null) return false;

        return property.GetValue(features) is true;
    }

    #endregion
}

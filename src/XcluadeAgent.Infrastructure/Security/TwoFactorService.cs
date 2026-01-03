using System.Security.Cryptography;
using OtpNet;

namespace XcluadeAgent.Infrastructure.Security;

/// <summary>
/// Service for handling Two-Factor Authentication (TOTP)
/// </summary>
public interface ITwoFactorService
{
    /// <summary>
    /// Generates a new TOTP secret key
    /// </summary>
    string GenerateSecretKey();

    /// <summary>
    /// Generates a QR code URI for authenticator apps
    /// </summary>
    string GenerateQrCodeUri(string secret, string email, string issuer = "XcluadeAgent");

    /// <summary>
    /// Validates a TOTP code against the secret
    /// </summary>
    bool ValidateCode(string secret, string code);

    /// <summary>
    /// Generates backup codes for account recovery
    /// </summary>
    List<string> GenerateBackupCodes(int count = 8);
}

/// <summary>
/// TOTP-based Two-Factor Authentication service
/// </summary>
public class TwoFactorService : ITwoFactorService
{
    private const int SecretKeyLength = 20;
    private const int CodeValidityWindow = 1; // Accept codes within +/- 1 time step (30 seconds each)

    public string GenerateSecretKey()
    {
        var secretBytes = new byte[SecretKeyLength];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(secretBytes);
        return Base32Encoding.ToString(secretBytes);
    }

    public string GenerateQrCodeUri(string secret, string email, string issuer = "XcluadeAgent")
    {
        // Format: otpauth://totp/ISSUER:EMAIL?secret=SECRET&issuer=ISSUER&algorithm=SHA1&digits=6&period=30
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedEmail = Uri.EscapeDataString(email);

        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={secret}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period=30";
    }

    public bool ValidateCode(string secret, string code)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(code))
            return false;

        // Remove any spaces or dashes from the code
        code = code.Replace(" ", "").Replace("-", "");

        if (code.Length != 6 || !code.All(char.IsDigit))
            return false;

        try
        {
            var secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes, step: 30, mode: OtpHashMode.Sha1, totpSize: 6);

            // Verify with a time window to handle clock drift
            return totp.VerifyTotp(code, out _, new VerificationWindow(CodeValidityWindow, CodeValidityWindow));
        }
        catch
        {
            return false;
        }
    }

    public List<string> GenerateBackupCodes(int count = 8)
    {
        var codes = new List<string>();
        using var rng = RandomNumberGenerator.Create();

        for (int i = 0; i < count; i++)
        {
            var codeBytes = new byte[4];
            rng.GetBytes(codeBytes);
            var codeNumber = BitConverter.ToUInt32(codeBytes, 0) % 100000000; // 8-digit code
            codes.Add(codeNumber.ToString("D8"));
        }

        return codes;
    }
}

namespace XcluadeAgent.Shared.Security;

/// <summary>
/// Utility class for validating file paths to prevent path traversal attacks
/// </summary>
public static class PathValidator
{
    /// <summary>
    /// List of allowed base directories for project paths
    /// </summary>
    private static readonly string[] AllowedBasePaths =
    [
        "/var/www",
        "/home",
        "/srv",
        "/opt",
        "/data"
    ];

    /// <summary>
    /// Validates that a path is safe and doesn't contain path traversal sequences
    /// </summary>
    /// <param name="path">The path to validate</param>
    /// <returns>True if the path is safe, false otherwise</returns>
    public static bool IsSafePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // Check for path traversal sequences
        if (path.Contains("..") ||
            path.Contains("./") ||
            path.Contains("/.") ||
            path.Contains("~"))
            return false;

        // Check for null bytes (common attack vector)
        if (path.Contains('\0'))
            return false;

        // Normalize the path and ensure it's absolute
        try
        {
            var normalizedPath = Path.GetFullPath(path);

            // Ensure the normalized path doesn't escape to a different location
            if (!normalizedPath.StartsWith(path.TrimEnd('/').TrimEnd('\\')))
            {
                // Path was normalized to a different location (traversal detected)
                return false;
            }
        }
        catch
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that a path is within allowed base directories
    /// </summary>
    /// <param name="path">The path to validate</param>
    /// <param name="customAllowedPaths">Optional custom allowed base paths</param>
    /// <returns>True if path is within allowed directories, false otherwise</returns>
    public static bool IsPathInAllowedDirectory(string path, IEnumerable<string>? customAllowedPaths = null)
    {
        if (!IsSafePath(path))
            return false;

        try
        {
            var normalizedPath = Path.GetFullPath(path);
            var allowedPaths = customAllowedPaths ?? AllowedBasePaths;

            return allowedPaths.Any(allowed =>
                normalizedPath.StartsWith(Path.GetFullPath(allowed), StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates that a relative path doesn't escape the base directory
    /// </summary>
    /// <param name="basePath">The base directory path</param>
    /// <param name="relativePath">The relative path within the base directory</param>
    /// <returns>True if the combined path stays within basePath, false otherwise</returns>
    public static bool IsRelativePathSafe(string basePath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(basePath) || string.IsNullOrWhiteSpace(relativePath))
            return false;

        try
        {
            var normalizedBase = Path.GetFullPath(basePath);
            var combinedPath = Path.GetFullPath(Path.Combine(basePath, relativePath));

            // Ensure the combined path starts with the base path
            return combinedPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sanitizes a file name by removing dangerous characters
    /// </summary>
    /// <param name="fileName">The file name to sanitize</param>
    /// <returns>Sanitized file name</returns>
    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());

        // Remove leading/trailing dots and spaces
        sanitized = sanitized.Trim('.', ' ');

        // Ensure it's not empty after sanitization
        return string.IsNullOrWhiteSpace(sanitized) ? "unnamed" : sanitized;
    }

    /// <summary>
    /// Gets a validated and normalized full path, throwing if invalid
    /// </summary>
    /// <param name="path">The path to validate and normalize</param>
    /// <param name="allowedBasePaths">Optional allowed base paths</param>
    /// <returns>The normalized full path</returns>
    /// <exception cref="ArgumentException">Thrown when the path is invalid or not in allowed directories</exception>
    public static string GetValidatedPath(string path, IEnumerable<string>? allowedBasePaths = null)
    {
        if (!IsSafePath(path))
            throw new ArgumentException($"Path contains invalid characters or traversal sequences: {path}", nameof(path));

        if (!IsPathInAllowedDirectory(path, allowedBasePaths))
            throw new ArgumentException($"Path is not in an allowed directory: {path}", nameof(path));

        return Path.GetFullPath(path);
    }
}

using System.Security.Cryptography;
using System.Text;

namespace AiEnterprise.Shared.Utilities;

public static class SecurityUtility
{
    /// <summary>
    /// Computes a SHA-256 hash of the input, used for audit entry integrity verification.
    /// </summary>
    public static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Hashes a password using PBKDF2 with SHA-256.
    /// </summary>
    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verifies a password against a stored hash.
    /// </summary>
    public static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    /// <summary>
    /// Generates a cryptographically secure random token.
    /// </summary>
    public static string GenerateSecureToken(int byteLength = 32)
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength));

    /// <summary>
    /// Computes a tamper-evident integrity hash for an audit log entry.
    /// </summary>
    public static string ComputeAuditIntegrityHash(
        Guid entryId,
        Guid enterpriseId,
        string action,
        string resourceId,
        string description,
        DateTime occurredAt)
    {
        var payload = $"{entryId}|{enterpriseId}|{action}|{resourceId}|{description}|{occurredAt:O}";
        return ComputeSha256Hash(payload);
    }

    /// <summary>
    /// Sanitizes a file name to prevent path traversal attacks.
    /// </summary>
    public static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(fileName.Where(c => !invalid.Contains(c)));
        return sanitized.Length > 200 ? sanitized[..200] : sanitized;
    }
}

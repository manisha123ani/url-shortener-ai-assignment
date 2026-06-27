namespace UrlShortener.Api.Services;

public static class UrlValidator
{
    private static readonly string[] AllowedSchemes = { "http", "https" };

    /// <summary>
    /// Validates that a string is a well-formed absolute http/https URL.
    /// Deliberately rejects other schemes (javascript:, data:, file:, ftp:) to prevent
    /// open-redirect / XSS-via-shortener abuse. See ARCHITECTURE.md "Security".
    /// </summary>
    public static bool TryValidate(string? url, out Uri? validUri, out string? error)
    {
        validUri = null;
        error = null;

        if (string.IsNullOrWhiteSpace(url))
        {
            error = "URL must not be empty.";
            return false;
        }

        if (url.Length > 2048)
        {
            error = "URL exceeds maximum length of 2048 characters.";
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            error = "URL is not a well-formed absolute URI.";
            return false;
        }

        if (!AllowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            error = $"Scheme '{uri.Scheme}' is not allowed. Only http/https are permitted.";
            return false;
        }

        // Basic SSRF guard for the prototype: block obviously internal hosts.
        // Production would resolve DNS and block private/link-local IP ranges too.
        var host = uri.Host.ToLowerInvariant();
        if (host is "localhost" or "0.0.0.0" || host.StartsWith("127.") || host.StartsWith("169.254."))
        {
            error = "URLs pointing to local/internal hosts are not allowed.";
            return false;
        }

        validUri = uri;
        return true;
    }

    private static readonly char[] AliasAllowedExtras = { '-', '_' };

    public static bool IsValidAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias) || alias.Length < 3 || alias.Length > 16) return false;
        return alias.All(c => char.IsLetterOrDigit(c) || AliasAllowedExtras.Contains(c));
    }
}

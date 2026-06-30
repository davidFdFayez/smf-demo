using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMF.Application.Common.Interfaces;

namespace SMF.Infrastructure.Compliance;

/// <summary>
/// Local-disk implementation of <see cref="IFileStorage"/>. Files land
/// under the configured root and are served back via the API's static-file
/// middleware. Suitable for development and small single-host deployments
/// — swap to an S3/Azure adapter for HA / multi-region clustering.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly IOptionsMonitor<ComplianceOptions> _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<LocalFileStorage> _logger;

    public LocalFileStorage(
        IOptionsMonitor<ComplianceOptions> options,
        IHostEnvironment env,
        ILogger<LocalFileStorage> logger)
    {
        _options = options; _env = env; _logger = logger;
    }

    public async Task<StoredFile> UploadAsync(
        Stream content, string folder, string originalFileName, string contentType, CancellationToken ct = default)
    {
        var safeFolder = Sanitize(folder);
        var ext = Path.GetExtension(originalFileName);
        var key = $"{safeFolder}/{Guid.NewGuid():N}{ext}";
        var absolute = ResolveAbsolutePath(key);

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);

        // Hash while we write so we don't have to re-read the file. The
        // SHA-256 lands on the metadata row for tamper-evidence.
        await using var fs = new FileStream(absolute, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var sha = SHA256.Create();

        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await content.ReadAsync(buffer.AsMemory(), ct)) > 0)
        {
            sha.TransformBlock(buffer, 0, read, null, 0);
            await fs.WriteAsync(buffer.AsMemory(0, read), ct);
            total += read;
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var hash = Convert.ToHexString(sha.Hash!).ToLowerInvariant();

        _logger.LogInformation(
            "Stored governance file {Key} ({Size} bytes, sha256={Hash})", key, total, hash);

        return new StoredFile(key, originalFileName, contentType, total, hash);
    }

    public Task<StoredFileStream?> OpenAsync(string key, CancellationToken ct = default)
    {
        var path = ResolveAbsolutePath(key);
        if (!File.Exists(path)) return Task.FromResult<StoredFileStream?>(null);

        var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var contentType = GuessContentType(path);
        var name = Path.GetFileName(path);
        return Task.FromResult<StoredFileStream?>(new StoredFileStream(fs, contentType, name, fs.Length));
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = ResolveAbsolutePath(key);
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete file {Key}", key); }
        return Task.CompletedTask;
    }

    public string GetPublicUrl(string key)
    {
        var prefix = _options.CurrentValue.Storage.PublicUrlPrefix.TrimEnd('/');
        return $"{prefix}/{key}";
    }

    private string ResolveAbsolutePath(string key)
    {
        var root = _options.CurrentValue.Storage.LocalRoot;
        var baseDir = Path.IsPathRooted(root)
            ? root
            : Path.Combine(_env.ContentRootPath ?? AppContext.BaseDirectory, root);
        var combined = Path.Combine(baseDir, key);
        var fullBase = Path.GetFullPath(baseDir);
        var fullCombined = Path.GetFullPath(combined);

        // Defensive — refuse to resolve outside the configured root even
        // if a malformed key contains "..". The application keys are
        // server-generated GUIDs so this is just belt-and-braces.
        if (!fullCombined.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException($"Path escape detected for key '{key}'.");

        return fullCombined;
    }

    private static string Sanitize(string folder) =>
        new(folder.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '/').ToArray());

    private static string GuessContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf"  => "application/pdf",
        ".doc"  => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls"  => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".png"  => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _       => "application/octet-stream"
    };
}

namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Storage abstraction for binary uploads (governance documents, future
/// safeguarding evidence). Providers are pluggable — local disk in dev,
/// S3/Azure Blob in production. Keys are opaque strings the storage
/// adapter chooses; the application stores the key and asks the adapter
/// to translate it back into a download URL when needed.
/// </summary>
public interface IFileStorage
{
    /// <summary>Persist a file. The adapter is free to namespace under
    /// <paramref name="folder"/>; <paramref name="originalFileName"/> is
    /// used only for content-disposition on download (not for routing).</summary>
    Task<StoredFile> UploadAsync(
        Stream content,
        string folder,
        string originalFileName,
        string contentType,
        CancellationToken ct = default);

    /// <summary>Open an existing file for streaming back to the client.
    /// Returns null if the key isn't found; callers translate this to 404.</summary>
    Task<StoredFileStream?> OpenAsync(string key, CancellationToken ct = default);

    /// <summary>Best-effort delete; missing keys are not an error.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>Public download URL. For local-disk this is a relative
    /// path; for cloud adapters it can be a signed URL with TTL.</summary>
    string GetPublicUrl(string key);
}

public sealed record StoredFile(
    string Key,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256);

public sealed record StoredFileStream(
    Stream Content,
    string ContentType,
    string OriginalFileName,
    long SizeBytes);

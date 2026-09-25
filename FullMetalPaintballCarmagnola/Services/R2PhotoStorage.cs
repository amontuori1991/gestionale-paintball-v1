using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Full_Metal_Paintball_Carmagnola.Models;
using System.Net;

namespace Full_Metal_Paintball_Carmagnola.Services;

public interface IPhotoStorage
{
    bool Configured { get; }
    Task<IReadOnlyList<AlbumPhoto>> List(Guid album, CancellationToken ct);
    Task Put(Guid album, Guid photo, byte[] jpeg, CancellationToken ct);
    Task<string?> DownloadUrl(Guid album, Guid photo, bool attachment, CancellationToken ct);
    Task Delete(Guid album, Guid photo, CancellationToken ct);
}

public sealed class R2PhotoStorage : IPhotoStorage, IDisposable
{
    private readonly IAmazonS3? client;
    private readonly string bucket;
    private readonly TimeProvider clock;
    public bool Configured => client != null;

    public R2PhotoStorage(IAmazonS3 client, string bucket, TimeProvider clock)
    {
        this.client = client;
        this.bucket = bucket;
        this.clock = clock;
    }

    public R2PhotoStorage(IConfiguration config, TimeProvider clock)
    {
        this.clock = clock;
        bucket = config["R2:BucketName"] ?? "";
        var key = config["R2:AccessKeyId"];
        var secret = config["R2:SecretAccessKey"];
        var endpoint = config["R2:ServiceUrl"];
        if (!string.IsNullOrWhiteSpace(bucket) && !string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(secret)
            && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme == "https"
            && uri.Host.EndsWith(".r2.cloudflarestorage.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.UserInfo))
        {
            client = new AmazonS3Client(new BasicAWSCredentials(key, secret), new AmazonS3Config
            {
                ServiceURL = endpoint, ForcePathStyle = true, AuthenticationRegion = "auto",
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                Timeout = TimeSpan.FromSeconds(45), MaxErrorRetry = 1
            });
        }
    }

    private IAmazonS3 Client => client ?? throw new InvalidOperationException("Archivio foto non configurato.");
    private static string Prefix(Guid album) => $"albums/{album:N}/";
    private static string Key(Guid album, Guid photo) => $"{Prefix(album)}{photo:N}.jpg";

    public async Task<IReadOnlyList<AlbumPhoto>> List(Guid album, CancellationToken ct)
    {
        var photos = new List<AlbumPhoto>();
        string? continuation = null;
        do
        {
            var response = await Client.ListObjectsV2Async(new ListObjectsV2Request
            { BucketName = bucket, Prefix = Prefix(album), ContinuationToken = continuation }, ct);
            foreach (var item in response.S3Objects)
            {
                if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(item.Key), "N", out var id))
                {
                    var photo = new AlbumPhoto(id, new DateTimeOffset(item.LastModified.ToUniversalTime()), item.Size);
                    if (photo.IsAvailable(clock.GetUtcNow())) photos.Add(photo);
                }
            }
            continuation = response.IsTruncated ? response.NextContinuationToken : null;
        } while (continuation != null);
        return photos.OrderBy(p => p.UploadedAt).ToList();
    }

    public async Task Put(Guid album, Guid photo, byte[] jpeg, CancellationToken ct)
    {
        using var stream = new MemoryStream(jpeg);
        await Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket, Key = Key(album, photo), InputStream = stream, ContentType = "image/jpeg",
            Headers = { CacheControl = "private, no-store" },
            DisablePayloadSigning = true, DisableDefaultChecksumValidation = true
        }, ct);
    }

    public async Task<string?> DownloadUrl(Guid album, Guid photo, bool attachment, CancellationToken ct)
    {
        try
        {
            var metadata = await Client.GetObjectMetadataAsync(bucket, Key(album, photo), ct);
            var uploaded = new DateTimeOffset(metadata.LastModified.ToUniversalTime());
            var expires = uploaded.AddDays(7);
            var now = clock.GetUtcNow();
            // Signed URLs also expire no later than the photo itself, even if lifecycle cleanup is delayed.
            if (expires <= now.AddSeconds(1)) return null;
            return Client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = bucket, Key = Key(album, photo), Verb = HttpVerb.GET,
                Expires = (expires < now.AddMinutes(5) ? expires : now.AddMinutes(5)).UtcDateTime,
                ResponseHeaderOverrides = new ResponseHeaderOverrides
                {
                    ContentType = "image/jpeg", CacheControl = "private, no-store",
                    ContentDisposition = attachment ? $"attachment; filename=FullMetal-{photo:N}.jpg" : "inline"
                }
            });
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound) { return null; }
    }

    public async Task Delete(Guid album, Guid photo, CancellationToken ct) =>
        await Client.DeleteObjectAsync(bucket, Key(album, photo), ct);

    public void Dispose() => client?.Dispose();
}

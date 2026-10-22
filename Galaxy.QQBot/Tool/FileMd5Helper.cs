using System.Security.Cryptography;

public static class FileMd5Helper
{
    public const long DefaultMaxBytes = 10_002_432;
    private const int BufferSize = 81920; // 80KB

    public static async Task<string> GetPartialMd5Async(string filePath, long? maxBytes = DefaultMaxBytes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路径不能为空", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("文件不存在", filePath);
        }

        long limit = (maxBytes is null || maxBytes <= 0) ? long.MaxValue : maxBytes.Value;

        using var md5 = MD5.Create();
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        long remaining = Math.Min(fs.Length, limit);
        byte[] buffer = new byte[BufferSize];
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int toRead = (int)Math.Min(buffer.Length, remaining);
            int read = await fs.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            md5.TransformBlock(buffer, 0, read, null, 0);
            remaining -= read;
        }

        md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return ToHexLower(md5.Hash!);
    }

    public static async Task<string> GetFullMd5Async(string filePath, CancellationToken cancellationToken = default)
    {
        return await GetPartialMd5Async(filePath, maxBytes: null, cancellationToken);
    }

    private static string ToHexLower(byte[] hash)
    {
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
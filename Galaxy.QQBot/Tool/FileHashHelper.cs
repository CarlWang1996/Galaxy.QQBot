using System.Security.Cryptography;

public static class FileHashHelper
{
    public const long DefaultMaxBytes = 10_002_432;
    private const int BufferSize = 81920;

    public static async Task<string> GetFileSha1Async(string filePath, CancellationToken cancellationToken = default)
    {
        using var sha1 = SHA1.Create();
        await HashPartialAsync(sha1, filePath, maxBytes: null, cancellationToken).ConfigureAwait(false);
        return ToHexLower(sha1.Hash!);
    }

    private static async Task HashPartialAsync(HashAlgorithm algorithm, string filePath, long? maxBytes, CancellationToken cancellationToken)
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

            algorithm.TransformBlock(buffer, 0, read, null, 0);
            remaining -= read;
        }

        algorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
    }

    private static string ToHexLower(byte[] hash)
    {
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
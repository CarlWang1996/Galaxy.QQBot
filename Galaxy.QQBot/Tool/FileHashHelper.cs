using System.Security.Cryptography;

namespace Galaxy.QQBot.Tool;

internal static class FileHashHelper
{
    public const long DefaultMaxBytes = 10_002_432;
    private const int BufferSize = 81920;

    public readonly record struct FileHashes(string Md5, string Sha1, string Md510m);

    public static async Task<FileHashes> ComputeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路径不能为空", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("文件不存在", filePath);
        }

        using var md5 = MD5.Create();
        using var sha1 = SHA1.Create();
        using var partialMd5 = MD5.Create();
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        long partialRemaining = Math.Min(fs.Length, DefaultMaxBytes);
        byte[] buffer = new byte[BufferSize];

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int read = await fs.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            md5.TransformBlock(buffer, 0, read, null, 0);
            sha1.TransformBlock(buffer, 0, read, null, 0);

            if (partialRemaining > 0)
            {
                int partialCount = (int)Math.Min(read, partialRemaining);
                partialMd5.TransformBlock(buffer, 0, partialCount, null, 0);
                partialRemaining -= partialCount;
            }
        }

        md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        partialMd5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

        return new FileHashes(
            ToHexLower(md5.Hash!),
            ToHexLower(sha1.Hash!),
            ToHexLower(partialMd5.Hash!));
    }

    private static string ToHexLower(byte[] hash)
    {
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

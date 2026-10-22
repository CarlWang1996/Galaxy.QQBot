using System.Security.Cryptography;

/// <summary>
/// 文件哈希工具类：MD5（前 N 字节） + SHA1（整个文件）
/// </summary>
public static class FileHashHelper
{
    /// <summary>默认只计算文件前 10,002,432 字节（约 9.54 MB）的 MD5</summary>
    public const long DefaultMaxBytes = 10_002_432;

    private const int BufferSize = 81920; // 80KB

    // ============================ MD5（前 N 字节） ============================

    /// <summary>
    /// 异步计算文件前 maxBytes 字节的 MD5。
    /// maxBytes 为 null 或 &lt;=0 时读取整个文件。
    /// </summary>
    public static async Task<string> GetPartialMd5Async(
        string filePath,
        long? maxBytes = DefaultMaxBytes,
        CancellationToken cancellationToken = default)
    {
        using var md5 = MD5.Create();
        await HashPartialAsync(md5, filePath, maxBytes, cancellationToken).ConfigureAwait(false);
        return ToHexLower(md5.Hash!);
    }

    /// <summary>同步计算文件前 maxBytes 字节的 MD5</summary>
    public static string GetPartialMd5(
        string filePath,
        long? maxBytes = DefaultMaxBytes,
        CancellationToken cancellationToken = default)
        => GetPartialMd5Async(filePath, maxBytes, cancellationToken).GetAwaiter().GetResult();

    // ============================ SHA1（整个文件） ============================

    /// <summary>异步计算整个文件的 SHA1</summary>
    public static async Task<string> GetFileSha1Async(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        using var sha1 = SHA1.Create();
        await HashPartialAsync(sha1, filePath, maxBytes: null, cancellationToken)
            .ConfigureAwait(false);
        return ToHexLower(sha1.Hash!);
    }

    /// <summary>同步计算整个文件的 SHA1</summary>
    public static string GetFileSha1(
        string filePath,
        CancellationToken cancellationToken = default)
        => GetFileSha1Async(filePath, cancellationToken).GetAwaiter().GetResult();

    // ============================ 内部公共逻辑 ============================

    /// <summary>
    /// 把文件前 maxBytes 字节（null 或 &lt;=0 表示整个文件）喂给哈希算法。
    /// 只负责 TransformBlock/TransformFinalBlock，不读取 Hash 属性。
    /// </summary>
    private static async Task HashPartialAsync(
        HashAlgorithm algorithm,
        string filePath,
        long? maxBytes,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("文件路径不能为空", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("文件不存在", filePath);

        long limit = (maxBytes is null || maxBytes <= 0) ? long.MaxValue : maxBytes.Value;

        using var fs = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        long remaining = Math.Min(fs.Length, limit);
        byte[] buffer = new byte[BufferSize];

        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int toRead = (int)Math.Min(buffer.Length, remaining);
            int read = await fs.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken)
                                .ConfigureAwait(false);
            if (read == 0) break; // 文件被截断

            algorithm.TransformBlock(buffer, 0, read, null, 0);
            remaining -= read;
        }

        algorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
    }

    private static string ToHexLower(byte[] hash)
    {
#if NET5_0_OR_GREATER
        return Convert.ToHexString(hash).ToLowerInvariant();
#else
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
#endif
    }
}
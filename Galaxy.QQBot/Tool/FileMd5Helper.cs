using System.Security.Cryptography;

/// <summary>
/// 文件 MD5 工具类
/// </summary>
public static class FileMd5Helper
{
    /// <summary>默认只计算文件前 10,002,432 字节（约 9.54 MB）的 MD5</summary>
    public const long DefaultMaxBytes = 10_002_432;

    private const int BufferSize = 81920; // 80KB

    /// <summary>
    /// 异步计算文件前 maxBytes 字节的 MD5。
    /// 文件长度不足 maxBytes 时按实际长度计算。
    /// </summary>
    /// <param name="filePath">文件完整路径</param>
    /// <param name="maxBytes">最多读取的字节数；传 null 或 &lt;=0 表示读取整个文件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>小写十六进制 MD5 字符串</returns>
    public static async Task<string> GetPartialMd5Async(
        string filePath,
        long? maxBytes = DefaultMaxBytes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("文件路径不能为空", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("文件不存在", filePath);

        // maxBytes <= 0 视为读取整个文件
        long limit = (maxBytes is null || maxBytes <= 0) ? long.MaxValue : maxBytes.Value;

        using var md5 = MD5.Create();
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
            if (read == 0) break; // 文件被截断或读取结束

            md5.TransformBlock(buffer, 0, read, null, 0);
            remaining -= read;
        }

        md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

        return ToHexLower(md5.Hash!);
    }

    /// <summary>
    /// 同步版本（内部复用异步逻辑，适合控制台/后台线程）
    /// </summary>
    public static string GetPartialMd5(
        string filePath,
        long? maxBytes = DefaultMaxBytes,
        CancellationToken cancellationToken = default)
    {
        return GetPartialMd5Async(filePath, maxBytes, cancellationToken)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    /// 异步计算整个文件的 MD5（等价于 maxBytes = null）
    /// </summary>
    public static async Task<string> GetFullMd5Async(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return await GetPartialMd5Async(filePath, maxBytes: null, cancellationToken);
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
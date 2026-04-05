using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// Base class for <see cref="IStorageStrategy"/> implementations.
  /// Provides the <see cref="CommitWrite"/> helper that handles atomic writes and rolling backups,
  /// so subclasses only need to write to the temp path and call <see cref="CommitWrite"/>.
  /// </summary>
  public abstract class StorageStrategyBase : IStorageStrategy
  {
    /// <inheritdoc/>
    public abstract string FileExtension { get; }

    /// <inheritdoc/>
    public abstract void Write<T>(string path, T data);

    /// <inheritdoc/>
    public abstract void WriteVersioned<T>(string path, T data, int version);

    /// <inheritdoc/>
    public abstract T Read<T>(string path);

    /// <inheritdoc/>
    public abstract T ReadVersioned<T>(string path, int expectedVersion);

    /// <inheritdoc/>
    public abstract Task WriteAsync<T>(string path, T data, CancellationToken ct);

    /// <inheritdoc/>
    public abstract Task<T> ReadAsync<T>(string path, CancellationToken ct);

    /// <inheritdoc/>
    public abstract Task WriteVersionedAsync<T>(string path, T data, int version, CancellationToken ct);

    /// <inheritdoc/>
    public abstract Task<T> ReadVersionedAsync<T>(string path, int expectedVersion, CancellationToken ct);

    /// <inheritdoc/>
    public abstract T ReadFromBytes<T>(byte[] rawBytes);

    /// <summary>
    /// Atomically promotes a completed temp file to the target path, rotating the previous
    /// target to <c>path + ".bak"</c> first. Call this after all writes to the temp file are done.
    /// </summary>
    /// <param name="targetPath">The final destination path (not the temp path).</param>
    protected static void CommitWrite(string targetPath) => AtomicFileCommit.Apply(targetPath);
  }
}

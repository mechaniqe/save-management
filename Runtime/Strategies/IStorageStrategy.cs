using System.Threading;
using System.Threading.Tasks;

namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// Defines the serialization and I/O contract for a single storage format.
  /// Implement this interface to add a custom format (e.g., MessagePack, binary)
  /// and pass an instance to <see cref="SaveManager(IStorageStrategy)"/>.
  /// <para>
  /// Implementations are responsible for atomic writes (temp file + rename).
  /// Extend <see cref="StorageStrategyBase"/> to inherit the <c>CommitWrite</c> helper
  /// that handles temp-file + backup rename automatically.
  /// </para>
  /// <para>
  /// Throw <see cref="VersionMismatchException"/> (not a general exception) from
  /// <see cref="ReadVersioned{T}"/> and <see cref="ReadVersionedAsync{T}"/> when the on-disk version does not match — this
  /// prevents <see cref="SaveManager"/> from firing <see cref="SaveManager.OnError"/>
  /// for an expected condition.
  /// </para>
  /// </summary>
  public interface IStorageStrategy
  {
    /// <summary>File extension written by this strategy, without a leading dot (e.g., "json").</summary>
    string FileExtension { get; }

    /// <summary>Serializes <paramref name="data"/> and writes it to <paramref name="path"/>.</summary>
    void Write<T>(string path, T data);

    /// <summary>Serializes <paramref name="data"/> wrapped in a versioned envelope and writes it to <paramref name="path"/>.</summary>
    void WriteVersioned<T>(string path, T data, int version);

    /// <summary>Reads and deserializes the file at <paramref name="path"/>.</summary>
    T Read<T>(string path);

    /// <summary>
    /// Reads and deserializes the versioned file at <paramref name="path"/>.
    /// Throws <see cref="VersionMismatchException"/> if the stored version differs from <paramref name="expectedVersion"/>.
    /// </summary>
    T ReadVersioned<T>(string path, int expectedVersion);

    /// <summary>Async version of <see cref="Write{T}"/>.</summary>
    Task WriteAsync<T>(string path, T data, CancellationToken ct);

    /// <summary>Async version of <see cref="Read{T}"/>.</summary>
    Task<T> ReadAsync<T>(string path, CancellationToken ct);

    /// <summary>Async version of <see cref="WriteVersioned{T}"/>.</summary>
    Task WriteVersionedAsync<T>(string path, T data, int version, CancellationToken ct);

    /// <summary>
    /// Async version of <see cref="ReadVersioned{T}"/>.
    /// Throws <see cref="VersionMismatchException"/> if the stored version differs from <paramref name="expectedVersion"/>.
    /// </summary>
    Task<T> ReadVersionedAsync<T>(string path, int expectedVersion, CancellationToken ct);

    /// <summary>
    /// Deserializes from raw bytes — used by <see cref="SaveManager.LoadFromResources{T}"/>.
    /// For text-based formats, decode the bytes as UTF-8.
    /// </summary>
    T ReadFromBytes<T>(byte[] rawBytes);
  }
}

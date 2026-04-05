using System.Threading;
using System.Threading.Tasks;

namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// Base class for strategies that persist data as JSON on disk (plain text or encrypted bytes).
  /// Uses the supplied <see cref="IJsonSerializer"/> and <see cref="JsonEnvelopeHelper"/> for payloads;
  /// subclasses define how the JSON string is encoded for storage and decoded back.
  /// </summary>
  public abstract class JsonStorageStrategyBase : StorageStrategyBase
  {
    private readonly IJsonSerializer _jsonSerializer;

    /// <param name="jsonSerializer">Serializer for JSON payloads. Must not be null.</param>
    protected JsonStorageStrategyBase(IJsonSerializer jsonSerializer)
    {
      _jsonSerializer = jsonSerializer ?? throw new System.ArgumentNullException(nameof(jsonSerializer));
    }

    /// <inheritdoc/>
    public override void Write<T>(string path, T data)
    {
      string json = _jsonSerializer.Serialize(data);
      WriteJsonToTemp(path, json);
      CommitWrite(path);
    }

    /// <inheritdoc/>
    public override void WriteVersioned<T>(string path, T data, int version)
    {
      string json = JsonEnvelopeHelper.SerializeVersionedEnvelope(_jsonSerializer, data, version);
      WriteJsonToTemp(path, json);
      CommitWrite(path);
    }

    /// <inheritdoc/>
    public override T Read<T>(string path)
    {
      string json = ReadJsonFromFile(path);
      return _jsonSerializer.Deserialize<T>(json);
    }

    /// <inheritdoc/>
    public override T ReadVersioned<T>(string path, int expectedVersion) =>
      JsonEnvelopeHelper.DeserializeVersionedPayload<T>(_jsonSerializer, ReadJsonFromFile(path), expectedVersion);

    /// <inheritdoc/>
    public override async Task WriteAsync<T>(string path, T data, CancellationToken ct)
    {
      string json = _jsonSerializer.Serialize(data);
      await WriteJsonToTempAsync(path, json, ct);
      CommitWrite(path);
    }

    /// <inheritdoc/>
    public override async Task<T> ReadAsync<T>(string path, CancellationToken ct)
    {
      string json = await ReadJsonFromFileAsync(path, ct);
      return _jsonSerializer.Deserialize<T>(json);
    }

    /// <inheritdoc/>
    public override async Task WriteVersionedAsync<T>(string path, T data, int version, CancellationToken ct)
    {
      string json = JsonEnvelopeHelper.SerializeVersionedEnvelope(_jsonSerializer, data, version);
      await WriteJsonToTempAsync(path, json, ct);
      CommitWrite(path);
    }

    /// <inheritdoc/>
    public override async Task<T> ReadVersionedAsync<T>(string path, int expectedVersion, CancellationToken ct)
    {
      string json = await ReadJsonFromFileAsync(path, ct);
      return JsonEnvelopeHelper.DeserializeVersionedPayload<T>(_jsonSerializer, json, expectedVersion);
    }

    /// <inheritdoc/>
    public override T ReadFromBytes<T>(byte[] rawBytes) =>
      _jsonSerializer.Deserialize<T>(ReadJsonFromBytes(rawBytes));

    /// <summary>
    /// Writes <paramref name="json"/> to <c>path + ".tmp"</c> using this strategy's encoding.
    /// </summary>
    protected abstract void WriteJsonToTemp(string path, string json);

    /// <summary>
    /// Reads the file at <paramref name="path"/> and returns the JSON string (decrypted if applicable).
    /// </summary>
    protected abstract string ReadJsonFromFile(string path);

    /// <summary>
    /// Async counterpart of <see cref="WriteJsonToTemp"/>.
    /// </summary>
    protected abstract Task WriteJsonToTempAsync(string path, string json, CancellationToken ct);

    /// <summary>
    /// Async counterpart of <see cref="ReadJsonFromFile"/>.
    /// </summary>
    protected abstract Task<string> ReadJsonFromFileAsync(string path, CancellationToken ct);

    /// <summary>
    /// Converts bundled resource bytes to a JSON string (e.g. UTF-8 text or decrypt).
    /// </summary>
    protected abstract string ReadJsonFromBytes(byte[] rawBytes);
  }
}

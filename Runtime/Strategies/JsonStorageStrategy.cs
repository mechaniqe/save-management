using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// <see cref="IStorageStrategy"/> implementation that serializes data as plain JSON
  /// using the configured <see cref="SaveManager.JsonSerializer"/>.
  /// </summary>
  public class JsonStorageStrategy : StorageStrategyBase
  {
    /// <inheritdoc/>
    public override string FileExtension => "json";

    /// <inheritdoc/>
    public override void Write<T>(string path, T data)
    {
      File.WriteAllText(path + ".tmp", SaveManager.JsonSerializer.Serialize(data));
      CommitWrite(path);
    }

    /// <inheritdoc/>
    public override void WriteVersioned<T>(string path, T data, int version)
    {
      File.WriteAllText(path + ".tmp", JsonEnvelopeHelper.SerializeVersionedEnvelope(data, version));
      CommitWrite(path);
    }

    /// <inheritdoc/>
    public override T Read<T>(string path)
    {
      return SaveManager.JsonSerializer.Deserialize<T>(File.ReadAllText(path));
    }

    /// <inheritdoc/>
    public override T ReadVersioned<T>(string path, int expectedVersion) =>
      JsonEnvelopeHelper.DeserializeVersionedPayload<T>(File.ReadAllText(path), expectedVersion);

    /// <inheritdoc/>
    public override async Task WriteAsync<T>(string path, T data, CancellationToken ct)
    {
      string serializedData = SaveManager.JsonSerializer.Serialize(data);
      // StreamWriter.WriteAsync(string, CancellationToken) requires .NET 5+.
      // On .NET Standard 2.0 (Unity) we check before opening the stream; the write itself is not interruptible.
      ct.ThrowIfCancellationRequested();
      using (StreamWriter writer = new StreamWriter(path + ".tmp", false))
      {
        await writer.WriteAsync(serializedData);
      }
      CommitWrite(path);
    }

    /// <inheritdoc/>
    public override async Task<T> ReadAsync<T>(string path, CancellationToken ct)
    {
      // StreamReader.ReadToEndAsync(CancellationToken) requires .NET 5+.
      // On .NET Standard 2.0 (Unity) we check before opening the stream; the read itself is not interruptible.
      ct.ThrowIfCancellationRequested();
      string serializedData;
      using (StreamReader reader = new StreamReader(path))
      {
        serializedData = await reader.ReadToEndAsync();
      }
      return SaveManager.JsonSerializer.Deserialize<T>(serializedData);
    }

    /// <inheritdoc/>
    public override T ReadFromBytes<T>(byte[] rawBytes)
    {
      return SaveManager.JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(rawBytes));
    }
  }
}

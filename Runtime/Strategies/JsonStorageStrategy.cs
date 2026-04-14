using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// <see cref="IStorageStrategy"/> implementation that serializes data as plain JSON
  /// using the <see cref="IJsonSerializer"/> supplied at construction (or <see cref="SaveManager.JsonSerializer"/> when omitted).
  /// </summary>
  public class JsonStorageStrategy : JsonStorageStrategyBase
  {
    /// <summary>
    /// Uses <see cref="SaveManager.JsonSerializer"/> as the JSON serializer.
    /// </summary>
    public JsonStorageStrategy()
      : this(null) { }

    /// <summary>
    /// <paramref name="jsonSerializer"/> if non-null; otherwise <see cref="SaveManager.JsonSerializer"/>.
    /// </summary>
    public JsonStorageStrategy(IJsonSerializer jsonSerializer)
      : base(jsonSerializer ?? SaveManager.JsonSerializer) { }

    /// <inheritdoc/>
    public override string FileExtension => "json";

    /// <inheritdoc cref="JsonStorageStrategyBase.WriteJsonToTemp"/>
    protected override void WriteJsonToTemp(string path, string json) =>
      File.WriteAllText(path + ".tmp", json);

    /// <inheritdoc cref="JsonStorageStrategyBase.ReadJsonFromFile"/>
    protected override string ReadJsonFromFile(string path) => File.ReadAllText(path);

    /// <inheritdoc cref="JsonStorageStrategyBase.WriteJsonToTempAsync"/>
    protected override async Task WriteJsonToTempAsync(string path, string json, CancellationToken ct)
    {
      // StreamWriter.WriteAsync(string, CancellationToken) requires .NET 5+.
      // On .NET Standard 2.0 (Unity) we check before opening the stream; the write itself is not interruptible.
      ct.ThrowIfCancellationRequested();
      using (StreamWriter writer = new StreamWriter(path + ".tmp", false))
      {
        await writer.WriteAsync(json).ConfigureAwait(false);
      }
    }

    /// <inheritdoc cref="JsonStorageStrategyBase.ReadJsonFromFileAsync"/>
    protected override async Task<string> ReadJsonFromFileAsync(string path, CancellationToken ct)
    {
      // StreamReader.ReadToEndAsync(CancellationToken) requires .NET 5+.
      // On .NET Standard 2.0 (Unity) we check before opening the stream; the read itself is not interruptible.
      ct.ThrowIfCancellationRequested();
      using (StreamReader reader = new StreamReader(path))
      {
        return await reader.ReadToEndAsync().ConfigureAwait(false);
      }
    }

    /// <inheritdoc cref="JsonStorageStrategyBase.ReadJsonFromBytes"/>
    protected override string ReadJsonFromBytes(byte[] rawBytes) =>
      Encoding.UTF8.GetString(rawBytes);
  }
}

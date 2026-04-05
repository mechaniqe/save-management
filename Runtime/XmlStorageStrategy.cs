using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// <see cref="IStorageStrategy"/> implementation that serializes data as XML
  /// using <see cref="System.Xml.Serialization.XmlSerializer"/>.
  /// Types must have a public parameterless constructor and public read/write properties or fields.
  /// </summary>
  public class XmlStorageStrategy : StorageStrategyBase
  {
    /// <inheritdoc/>
    public override string FileExtension => "xml";

    /// <inheritdoc/>
    public override void Write<T>(string path, T data)
    {
      XmlSerializer serializer = new XmlSerializer(typeof(T));
      using (FileStream stream = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write))
      {
        serializer.Serialize(stream, data);
      }
      CommitWrite(path);
    }

    /// <inheritdoc/>
    public override void WriteVersioned<T>(string path, T data, int version)
    {
      XmlSerializer serializer = new XmlSerializer(typeof(SaveEnvelope<T>));
      using (FileStream stream = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write))
      {
        serializer.Serialize(stream, new SaveEnvelope<T> { Version = version, Data = data });
      }
      CommitWrite(path);
    }

    /// <inheritdoc/>
    public override T Read<T>(string path)
    {
      XmlSerializer serializer = new XmlSerializer(typeof(T));
      using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
      {
        return (T)serializer.Deserialize(stream);
      }
    }

    /// <inheritdoc/>
    public override T ReadVersioned<T>(string path, int expectedVersion)
    {
      XmlSerializer serializer = new XmlSerializer(typeof(SaveEnvelope<T>));
      using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
      {
        SaveEnvelope<T> envelope = (SaveEnvelope<T>)serializer.Deserialize(stream);
        if (envelope.Version != expectedVersion)
          throw new VersionMismatchException();
        return envelope.Data;
      }
    }

    /// <inheritdoc/>
    public override Task WriteAsync<T>(string path, T data, CancellationToken ct)
    {
      return Task.Run(() => Write(path, data), ct);
    }

    /// <inheritdoc/>
    public override Task<T> ReadAsync<T>(string path, CancellationToken ct)
    {
      return Task.Run(() => Read<T>(path), ct);
    }

    /// <inheritdoc/>
    public override T ReadFromBytes<T>(byte[] rawBytes)
    {
      XmlSerializer serializer = new XmlSerializer(typeof(T));
      using (MemoryStream ms = new MemoryStream(rawBytes))
      {
        return (T)serializer.Deserialize(ms);
      }
    }
  }
}

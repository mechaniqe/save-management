namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// Shared JSON envelope read/write for <see cref="JsonStorageStrategy"/> and
  /// <see cref="EncryptedStorageStrategy"/> (JsonUtility cannot serialize open generic types,
  /// so the payload is a nested JSON string).
  /// </summary>
  internal static class JsonEnvelopeHelper
  {
    internal static string SerializeVersionedEnvelope<T>(T data, int version)
    {
      JsonEnvelope envelope = new JsonEnvelope
      {
        version = version,
        data = SaveManager.JsonSerializer.Serialize(data)
      };
      return SaveManager.JsonSerializer.Serialize(envelope);
    }

    internal static T DeserializeVersionedPayload<T>(string envelopeJson, int expectedVersion)
    {
      JsonEnvelope envelope = SaveManager.JsonSerializer.Deserialize<JsonEnvelope>(envelopeJson);
      if (envelope.version != expectedVersion)
        throw new VersionMismatchException();
      return SaveManager.JsonSerializer.Deserialize<T>(envelope.data);
    }
  }
}

namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// Shared JSON envelope read/write for <see cref="JsonStorageStrategy"/> and
  /// <see cref="EncryptedStorageStrategy"/> (JsonUtility cannot serialize open generic types,
  /// so the payload is a nested JSON string).
  /// </summary>
  internal static class JsonEnvelopeHelper
  {
    internal static string SerializeVersionedEnvelope<T>(IJsonSerializer jsonSerializer, T data, int version)
    {
      JsonEnvelope envelope = new JsonEnvelope
      {
        version = version,
        data = jsonSerializer.Serialize(data)
      };
      return jsonSerializer.Serialize(envelope);
    }

    internal static T DeserializeVersionedPayload<T>(IJsonSerializer jsonSerializer, string envelopeJson, int expectedVersion)
    {
      JsonEnvelope envelope = jsonSerializer.Deserialize<JsonEnvelope>(envelopeJson);
      if (envelope.version != expectedVersion)
        throw new VersionMismatchException();
      return jsonSerializer.Deserialize<T>(envelope.data);
    }
  }
}

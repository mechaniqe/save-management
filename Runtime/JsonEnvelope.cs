namespace DynamicBox.SaveManagement
{
  // DTO for versioned JSON saves; serialization paths live in JsonEnvelopeHelper.
  // JsonUtility cannot serialize open generic types, so the payload is serialized
  // separately and stored as a nested JSON string.
  [System.Serializable]
  internal class JsonEnvelope
  {
    public int version;
    public string data;
  }
}

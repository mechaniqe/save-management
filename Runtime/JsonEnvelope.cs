namespace DynamicBox.SaveManagement
{
  // Used by JsonStorageStrategy and EncryptedStorageStrategy for versioned saves.
  // JsonUtility cannot serialize open generic types, so the payload is serialized
  // separately and stored as a nested JSON string.
  [System.Serializable]
  internal class JsonEnvelope
  {
    public int version;
    public string data;
  }
}

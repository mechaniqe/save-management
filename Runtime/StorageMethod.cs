namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// Defines the serialization format used by <see cref="SaveManager"/> for all read and write operations.
  /// </summary>
  public enum StorageMethod
  {
    /// <summary>AES-256 encrypted JSON. Not human-readable or editable. Requires an encryption key in the <see cref="SaveManager"/> constructor.</summary>
    Encrypted,
    /// <summary>Human-readable and widely supported. Requires public fields or properties on saved types.</summary>
    XML,
    /// <summary>Human-readable and Unity-native via <c>JsonUtility</c>. Requires <c>[Serializable]</c> on saved types.</summary>
    JSON
  }
}
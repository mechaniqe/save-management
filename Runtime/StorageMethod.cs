namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// Defines the serialization format used by <see cref="SaveManager"/> for all read and write operations.
  /// </summary>
  public enum StorageMethod
  {
    /// <summary>Fast and compact, but not human-readable. Requires <c>[Serializable]</c> on saved types.</summary>
    Binary,
    /// <summary>Human-readable and widely supported. Requires public fields or properties on saved types.</summary>
    XML,
    /// <summary>Human-readable and Unity-native via <c>JsonUtility</c>. Requires <c>[Serializable]</c> on saved types.</summary>
    JSON
  }
}
namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// Creates <see cref="IStorageStrategy"/> instances from a <see cref="StorageMethod"/> value.
  /// Centralises strategy construction so that <see cref="SaveManager"/> remains stable
  /// when new formats are added.
  /// </summary>
  public static class StorageStrategyFactory
  {
    /// <summary>
    /// Returns an <see cref="IStorageStrategy"/> for the given <paramref name="method"/>.
    /// </summary>
    /// <param name="method">The serialization format to create a strategy for.</param>
    /// <param name="encryptionKey">
    /// Required when <paramref name="method"/> is <see cref="StorageMethod.Encrypted"/>.
    /// Any string length is accepted — the key is hashed to a fixed size internally.
    /// </param>
    /// <param name="jsonSerializer">
    /// Used for JSON-based strategies. If null, <see cref="SaveManager.JsonSerializer"/> is used.
    /// </param>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="method"/> is <see cref="StorageMethod.Encrypted"/>
    /// and <paramref name="encryptionKey"/> is null or empty.
    /// </exception>
    public static IStorageStrategy Create(StorageMethod method, string encryptionKey = null, IJsonSerializer jsonSerializer = null)
    {
      IJsonSerializer serializer = jsonSerializer ?? SaveManager.JsonSerializer;
      switch (method)
      {
        case StorageMethod.Encrypted:
          if (string.IsNullOrEmpty(encryptionKey))
            throw new System.ArgumentException(
              "An encryption key must be provided when using StorageMethod.Encrypted.", nameof(encryptionKey));
          return new EncryptedStorageStrategy(encryptionKey, serializer);
        case StorageMethod.XML:
          return new XmlStorageStrategy();
        default:
          return new JsonStorageStrategy(serializer);
      }
    }
  }
}

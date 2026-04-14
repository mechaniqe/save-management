namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// Thrown by <see cref="IStorageStrategy.ReadVersioned{T}"/> and <see cref="IStorageStrategy.ReadVersionedAsync{T}"/> when the version stored on disk
  /// does not match the expected version. This is a normal condition handled silently by
  /// <see cref="SaveManager"/> — it is not reported through <see cref="SaveManager.OnError"/>.
  /// Custom <see cref="IStorageStrategy"/> implementations should throw this (not a generic
  /// exception) to signal version mismatch so callers receive the correct behaviour.
  /// </summary>
  public class VersionMismatchException : System.Exception
  {
    public VersionMismatchException()
      : base("The save file version does not match the expected version.") { }
  }
}

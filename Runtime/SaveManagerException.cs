namespace DynamicBox.SaveManagement
{
  /// <summary>The type of operation that triggered a <see cref="SaveManagerException"/>.</summary>
  public enum SaveOperation { Save, Load, Delete }

  /// <summary>
  /// Thrown — and passed to <see cref="SaveManager.OnError"/> — when a save, load,
  /// or delete operation fails. Wraps the original exception and adds context about
  /// which file was involved and what was being done.
  /// </summary>
  public class SaveManagerException : System.Exception
  {
    /// <summary>The file or resource name that was being accessed when the error occurred.</summary>
    public string DataName { get; }

    /// <summary>The operation that was in progress when the error occurred.</summary>
    public SaveOperation Operation { get; }

    public SaveManagerException(string message, string dataName, SaveOperation operation, System.Exception inner)
      : base(message, inner)
    {
      DataName = dataName;
      Operation = operation;
    }
  }
}

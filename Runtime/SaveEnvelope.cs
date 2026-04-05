namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// Wraps saved data with a schema version number. Used internally by the versioned
  /// overloads of <see cref="SaveManager.SaveToFile{T}(T,string,int)"/> and
  /// <see cref="SaveManager.LoadFromFile{T}(string,T,int)"/> for XML serialization.
  /// </summary>
  [System.Serializable]
  public class SaveEnvelope<T>
  {
    /// <summary>The schema version this save was written with.</summary>
    public int Version;

    /// <summary>The serialized payload.</summary>
    public T Data;
  }
}

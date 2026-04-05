namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// Defines the JSON serialization contract used by <see cref="SaveManager"/>.
  /// Implement this interface to replace the default <see cref="JsonUtilitySerializer"/>
  /// with a library of your choice (e.g., Newtonsoft.Json, System.Text.Json).
  /// <para>
  /// The default implementation requires types to carry the <c>[Serializable]</c> attribute
  /// and only supports public fields. Custom implementations may lift these restrictions.
  /// </para>
  /// </summary>
  public interface IJsonSerializer
  {
    string Serialize<T>(T obj);
    T Deserialize<T>(string json);
  }
}

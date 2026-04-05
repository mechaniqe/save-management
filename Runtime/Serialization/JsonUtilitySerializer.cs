using UnityEngine;

namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// Default <see cref="IJsonSerializer"/> implementation backed by Unity's <c>JsonUtility</c>.
  /// Requires types to carry the <c>[Serializable]</c> attribute and only supports public fields.
  /// </summary>
  public class JsonUtilitySerializer : IJsonSerializer
  {
    public string Serialize<T>(T obj) => JsonUtility.ToJson(obj, true);
    public T Deserialize<T>(string json) => JsonUtility.FromJson<T>(json);
  }
}

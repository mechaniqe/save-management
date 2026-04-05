namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// Authoritative ordered list of save slot directory names under persistent data.
  /// On disk as <c>slots.registry.json</c>; <see cref="SlotManager"/> is the only writer.
  /// Uses <see cref="SlotNameEntry"/> instead of <c>string[]</c> so Unity's <c>JsonUtility</c>
  /// can round-trip the file when <see cref="SaveManager.JsonSerializer"/> is the default.
  /// </summary>
  [System.Serializable]
  internal class SlotRegistryRoot
  {
    public int version = 1;
    public SlotNameEntry[] slots;
  }

  [System.Serializable]
  internal class SlotNameEntry
  {
    public string name;
  }
}

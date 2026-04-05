namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// Metadata for a save slot, readable without deserializing any game data.
  /// Useful for populating a save-select UI.
  /// </summary>
  [System.Serializable]
  public class SaveSlotInfo
  {
    /// <summary>The slot's directory name (e.g., "slot_1", "autosave").</summary>
    public string Name;

    /// <summary>
    /// Optional display label assigned by the game (e.g., "Chapter 3 - The Forest").
    /// Empty when no label has been set.
    /// </summary>
    public string Label;

    /// <summary>
    /// UTC timestamp of the last save operation, stored as an ISO 8601 string.
    /// Use <see cref="LastModifiedUtc"/> to get a parsed <see cref="System.DateTime"/>.
    /// </summary>
    public string LastModified;

    /// <summary>
    /// Parses <see cref="LastModified"/> into a UTC <see cref="System.DateTime"/>.
    /// Returns <see cref="System.DateTime.MinValue"/> if no save has been written yet.
    /// </summary>
    public System.DateTime LastModifiedUtc =>
      string.IsNullOrEmpty(LastModified)
        ? System.DateTime.MinValue
        : System.DateTime.Parse(LastModified, null,
            System.Globalization.DateTimeStyles.RoundtripKind);
  }
}

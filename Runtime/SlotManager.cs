using System.IO;
using UnityEngine;

namespace DynamicBox.SaveManagement
{
  internal class SlotManager
  {
    private readonly string _baseLocation;
    private string _activeSlot;

    internal string ActiveSlot => _activeSlot;

    internal SlotManager(string baseLocation)
    {
      _baseLocation = baseLocation;
    }

    internal string GetSaveDirectory() =>
      _activeSlot != null ? Path.Combine(_baseLocation, _activeSlot) : _baseLocation;

    internal void SetSlot(string slotName, string label = null)
    {
      if (string.IsNullOrWhiteSpace(slotName))
        throw new System.ArgumentException("Slot name cannot be null or whitespace.", nameof(slotName));
      _activeSlot = slotName;
      Directory.CreateDirectory(GetSaveDirectory());
      if (label != null)
        SetSlotLabel(label);
    }

    internal void ClearSlot() => _activeSlot = null;

    internal void SetSlotLabel(string label)
    {
      if (_activeSlot == null)
        throw new System.InvalidOperationException("No active slot. Call SetSlot first.");
      SaveSlotInfo info = ReadSlotMeta(_activeSlot) ?? new SaveSlotInfo { Name = _activeSlot };
      info.Label = label;
      WriteSlotMeta(info);
    }

    internal SaveSlotInfo[] ListSlots()
    {
      string[] dirs = Directory.GetDirectories(_baseLocation);
      SaveSlotInfo[] slots = new SaveSlotInfo[dirs.Length];
      for (int i = 0; i < dirs.Length; i++)
      {
        string name = Path.GetFileName(dirs[i]);
        slots[i] = ReadSlotMeta(name) ?? new SaveSlotInfo { Name = name };
      }
      return slots;
    }

    // Throws on failure — SaveManager catches and routes through OnError.
    internal void DeleteSlot(string slotName)
    {
      Directory.Delete(Path.Combine(_baseLocation, slotName), true);
    }

    internal void UpdateSlotMeta()
    {
      try
      {
        SaveSlotInfo info = ReadSlotMeta(_activeSlot) ?? new SaveSlotInfo { Name = _activeSlot };
        info.LastModified = System.DateTime.UtcNow.ToString("O");
        WriteSlotMeta(info);
      }
      catch (System.Exception ex)
      {
        Debug.LogWarning($"SaveManager: failed to update metadata for slot '{_activeSlot}': {ex.Message}");
      }
    }

    internal SaveSlotInfo ReadSlotMeta(string slotName)
    {
      string metaPath = Path.Combine(_baseLocation, slotName, "slot.meta");
      if (!File.Exists(metaPath))
        return null;
      try
      {
        return SaveManager.JsonSerializer.Deserialize<SaveSlotInfo>(File.ReadAllText(metaPath));
      }
      catch
      {
        return null;
      }
    }

    internal void WriteSlotMeta(SaveSlotInfo info)
    {
      File.WriteAllText(
        Path.Combine(_baseLocation, info.Name, "slot.meta"),
        SaveManager.JsonSerializer.Serialize(info));
    }
  }
}

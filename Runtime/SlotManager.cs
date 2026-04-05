using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DynamicBox.SaveManagement
{
  internal class SlotManager
  {
    private const string RegistryFileName = "slots.registry.json";

    private readonly string _baseLocation;
    private readonly string _registryPath;
    private readonly IJsonSerializer _jsonSerializer;
    private string _activeSlot;

    internal string ActiveSlot => _activeSlot;

    /// <summary>Root path passed at construction (typically <c>Application.persistentDataPath</c>).</summary>
    internal string PersistentDataRoot => _baseLocation;

    internal SlotManager(string baseLocation, IJsonSerializer jsonSerializer)
    {
      _baseLocation = baseLocation;
      _registryPath = Path.Combine(_baseLocation, RegistryFileName);
      _jsonSerializer = jsonSerializer ?? throw new System.ArgumentNullException(nameof(jsonSerializer));
    }

    internal string GetSaveDirectory() =>
      _activeSlot != null ? Path.Combine(_baseLocation, _activeSlot) : _baseLocation;

    internal void SetSlot(string slotName, string label = null)
    {
      if (string.IsNullOrWhiteSpace(slotName))
        throw new System.ArgumentException("Slot name cannot be null or whitespace.", nameof(slotName));
      _activeSlot = slotName;
      Directory.CreateDirectory(GetSaveDirectory());
      RegisterSlotName(slotName);
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

    /// <summary>
    /// Returns slots in registry order. The registry file is authoritative — directories not listed
    /// there are ignored. Registry entries without a folder still appear with minimal metadata.
    /// </summary>
    internal SaveSlotInfo[] ListSlots()
    {
      SlotRegistryRoot reg = ReadRegistry();
      string[] names = SlotNamesFromEntries(reg.slots);
      SaveSlotInfo[] slots = new SaveSlotInfo[names.Length];
      for (int i = 0; i < names.Length; i++)
      {
        string name = names[i];
        slots[i] = ReadSlotMeta(name) ?? new SaveSlotInfo { Name = name };
      }
      return slots;
    }

    // Throws on failure — SaveManager catches and routes through OnError.
    internal void DeleteSlot(string slotName)
    {
      RemoveSlotName(slotName);
      string dir = Path.Combine(_baseLocation, slotName);
      if (Directory.Exists(dir))
        Directory.Delete(dir, true);
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
        return _jsonSerializer.Deserialize<SaveSlotInfo>(File.ReadAllText(metaPath));
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
        _jsonSerializer.Serialize(info));
    }

    /// <summary>
    /// Ensures <see cref="_registryPath"/> exists and is valid. If missing, builds from existing
    /// subdirectories (one-time migration). If unreadable, rebuilds from disk.
    /// </summary>
    private void EnsureRegistryOnDisk()
    {
      if (!File.Exists(_registryPath))
      {
        WriteRegistryFile(BuildMigrationFromDirectories());
        return;
      }
      if (TryReadRegistryFromDisk() == null)
      {
        Debug.LogWarning("SaveManager: slot registry was unreadable; rebuilding from disk.");
        WriteRegistryFile(BuildMigrationFromDirectories());
      }
    }

    private SlotRegistryRoot ReadRegistry()
    {
      EnsureRegistryOnDisk();
      SlotRegistryRoot reg = TryReadRegistryFromDisk();
      if (reg == null)
        return new SlotRegistryRoot { version = 1, slots = System.Array.Empty<SlotNameEntry>() };
      string[] names = DeduplicatePreserveOrder(SlotNamesFromEntries(reg.slots));
      reg.slots = EntriesFromNames(names);
      return reg;
    }

    private SlotRegistryRoot BuildMigrationFromDirectories()
    {
      if (!Directory.Exists(_baseLocation))
        return new SlotRegistryRoot { version = 1, slots = System.Array.Empty<SlotNameEntry>() };

      string[] dirs = Directory.GetDirectories(_baseLocation);
      var names = new List<string>(dirs.Length);
      for (int i = 0; i < dirs.Length; i++)
      {
        string name = Path.GetFileName(dirs[i]);
        if (string.IsNullOrEmpty(name))
          continue;
        names.Add(name);
      }
      names.Sort(System.StringComparer.Ordinal);
      return new SlotRegistryRoot { version = 1, slots = EntriesFromNames(names.ToArray()) };
    }

    private SlotRegistryRoot TryReadRegistryFromDisk()
    {
      try
      {
        string json = File.ReadAllText(_registryPath);
        return _jsonSerializer.Deserialize<SlotRegistryRoot>(json);
      }
      catch
      {
        return null;
      }
    }

    private static string[] SlotNamesFromEntries(SlotNameEntry[] entries)
    {
      if (entries == null || entries.Length == 0)
        return System.Array.Empty<string>();
      var list = new List<string>(entries.Length);
      for (int i = 0; i < entries.Length; i++)
      {
        if (entries[i] == null || string.IsNullOrWhiteSpace(entries[i].name))
          continue;
        list.Add(entries[i].name.Trim());
      }
      return list.ToArray();
    }

    private static SlotNameEntry[] EntriesFromNames(string[] names)
    {
      if (names == null || names.Length == 0)
        return System.Array.Empty<SlotNameEntry>();
      var entries = new SlotNameEntry[names.Length];
      for (int i = 0; i < names.Length; i++)
        entries[i] = new SlotNameEntry { name = names[i] };
      return entries;
    }

    private static string[] DeduplicatePreserveOrder(string[] slots)
    {
      if (slots == null || slots.Length == 0)
        return System.Array.Empty<string>();
      var seen = new HashSet<string>(System.StringComparer.Ordinal);
      var list = new List<string>(slots.Length);
      for (int i = 0; i < slots.Length; i++)
      {
        string s = slots[i];
        if (string.IsNullOrWhiteSpace(s))
          continue;
        if (seen.Add(s))
          list.Add(s);
      }
      return list.ToArray();
    }

    private void RegisterSlotName(string slotName)
    {
      SlotRegistryRoot reg = ReadRegistry();
      string[] current = SlotNamesFromEntries(reg.slots);
      for (int i = 0; i < current.Length; i++)
      {
        if (string.Equals(current[i], slotName, System.StringComparison.Ordinal))
          return;
      }
      var next = new string[current.Length + 1];
      System.Array.Copy(current, next, current.Length);
      next[current.Length] = slotName;
      reg.slots = EntriesFromNames(next);
      WriteRegistryFile(reg);
    }

    private void RemoveSlotName(string slotName)
    {
      SlotRegistryRoot reg = ReadRegistry();
      string[] current = SlotNamesFromEntries(reg.slots);
      int keep = 0;
      for (int i = 0; i < current.Length; i++)
      {
        if (!string.Equals(current[i], slotName, System.StringComparison.Ordinal))
          keep++;
      }
      if (keep == current.Length)
        return;
      var next = new string[keep];
      int w = 0;
      for (int i = 0; i < current.Length; i++)
      {
        if (!string.Equals(current[i], slotName, System.StringComparison.Ordinal))
          next[w++] = current[i];
      }
      reg.slots = EntriesFromNames(next);
      WriteRegistryFile(reg);
    }

    private void WriteRegistryFile(SlotRegistryRoot reg)
    {
      if (reg == null)
        reg = new SlotRegistryRoot { version = 1, slots = System.Array.Empty<SlotNameEntry>() };
      reg.version = reg.version <= 0 ? 1 : reg.version;
      reg.slots = EntriesFromNames(DeduplicatePreserveOrder(SlotNamesFromEntries(reg.slots)));
      string json = _jsonSerializer.Serialize(reg);
      string tempPath = _registryPath + ".tmp";
      File.WriteAllText(tempPath, json);
      CommitRegistryWrite(_registryPath, tempPath);
    }

    private static void CommitRegistryWrite(string targetPath, string tempPath)
    {
      string backupPath = targetPath + ".bak";
      if (File.Exists(targetPath))
      {
        if (File.Exists(backupPath))
          File.Delete(backupPath);
        File.Move(targetPath, backupPath);
      }
      File.Move(tempPath, targetPath);
    }
  }
}

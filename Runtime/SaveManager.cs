using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// Facade for reading and writing persistent save data.
  /// Delegates serialization and I/O to an <see cref="IStorageStrategy"/> and slot
  /// management to an internal <see cref="SlotManager"/>.
  /// Subscribe to <see cref="OnError"/> to handle failures without polling return values.
  /// </summary>
  public class SaveManager
  {
    private readonly IStorageStrategy _strategy;
    private readonly SlotManager _slotManager;
    private readonly IJsonSerializer _jsonSerializer;

    /// <summary>
    /// The <see cref="IJsonSerializer"/> used by this instance for slot registry and metadata,
    /// and for JSON / encrypted save files when using the built-in strategies.
    /// </summary>
    public IJsonSerializer Serializer => _jsonSerializer;

    /// <summary>
    /// Fired whenever a save, load, or delete operation fails.
    /// Provides the file name, operation type, and original exception.
    /// </summary>
    public event System.Action<SaveManagerException> OnError;

    private static IJsonSerializer _defaultJsonSerializer = new JsonUtilitySerializer();
    private static bool _defaultJsonSerializerAssigned;

    /// <summary>
    /// Default JSON serializer used when a <see cref="SaveManager"/> is constructed without an explicit
    /// <see cref="IJsonSerializer"/> and when <see cref="JsonStorageStrategy"/> / <see cref="EncryptedStorageStrategy"/>
    /// are created without one. Defaults to <see cref="JsonUtilitySerializer"/>. Set once at startup before saves,
    /// or pass a serializer into <see cref="SaveManager(StorageMethod,string,IJsonSerializer)"/> per instance.
    /// Reassigning after saves exist on disk may make those saves unreadable with the new serializer.
    /// </summary>
    public static IJsonSerializer JsonSerializer
    {
      get => _defaultJsonSerializer;
      set
      {
        if (_defaultJsonSerializerAssigned)
          Debug.LogWarning("SaveManager.JsonSerializer was changed after already being assigned. Saves written with the previous serializer may no longer be readable.");
        _defaultJsonSerializer = value ?? throw new System.ArgumentNullException(nameof(value));
        _defaultJsonSerializerAssigned = true;
      }
    }

    /// <summary>
    /// Creates a new SaveManager using the specified serialization format.
    /// </summary>
    /// <param name="method">The format used for all read and write operations.</param>
    /// <param name="encryptionKey">
    /// Required when using <see cref="StorageMethod.Encrypted"/>. Any string length is accepted —
    /// the key is hashed to a fixed size internally. Keep this value consistent between saves and loads.
    /// </param>
    /// <param name="jsonSerializer">
    /// Serializer for slot metadata and JSON-based formats. If null, <see cref="JsonSerializer"/> (static default) is used.
    /// </param>
    /// <param name="persistentDataRoot">
    /// Root directory for slot registry, slot folders, and save files (same role as <c>Application.persistentDataPath</c>).
    /// If null or empty, <see cref="Application.persistentDataPath"/> is used. Pass a dedicated path for tests or tooling.
    /// </param>
    public SaveManager(StorageMethod method, string encryptionKey = null, IJsonSerializer jsonSerializer = null, string persistentDataRoot = null)
    {
      _jsonSerializer = jsonSerializer ?? JsonSerializer;
      _strategy = StorageStrategyFactory.Create(method, encryptionKey, _jsonSerializer);
      _slotManager = new SlotManager(ResolvePersistentRoot(persistentDataRoot), _jsonSerializer);
    }

    /// <summary>
    /// Creates a new SaveManager using a custom <see cref="IStorageStrategy"/>.
    /// Use this overload to supply a format not covered by <see cref="StorageMethod"/>.
    /// </summary>
    /// <param name="strategy">The strategy that handles serialization and file I/O.</param>
    /// <param name="jsonSerializer">
    /// Serializer for slot registry and <c>slot.meta</c> files. If null, <see cref="JsonSerializer"/> (static default) is used.
    /// Use the same <see cref="IJsonSerializer"/> your JSON-based strategy was built with, if applicable.
    /// </param>
    /// <param name="persistentDataRoot">
    /// Root directory for slot registry, slot folders, and save files. If null or empty, <see cref="Application.persistentDataPath"/> is used.
    /// </param>
    public SaveManager(IStorageStrategy strategy, IJsonSerializer jsonSerializer = null, string persistentDataRoot = null)
    {
      _jsonSerializer = jsonSerializer ?? JsonSerializer;
      _strategy = strategy ?? throw new System.ArgumentNullException(nameof(strategy));
      _slotManager = new SlotManager(ResolvePersistentRoot(persistentDataRoot), _jsonSerializer);
    }

    private static string ResolvePersistentRoot(string persistentDataRoot) =>
      string.IsNullOrEmpty(persistentDataRoot) ? Application.persistentDataPath : persistentDataRoot;

    // -------------------------------------------------------------------------
    // Slots
    // -------------------------------------------------------------------------

    /// <summary>
    /// The currently active slot name, or <c>null</c> if no slot is set.
    /// All file operations use this slot's subdirectory when set.
    /// </summary>
    public string ActiveSlot => _slotManager.ActiveSlot;

    /// <summary>
    /// Activates a save slot. All subsequent file operations will read and write from
    /// a subdirectory named <paramref name="slotName"/> inside <c>Application.persistentDataPath</c>.
    /// The directory is created if it does not exist.
    /// </summary>
    /// <param name="slotName">A unique name identifying the slot (e.g., "slot_1", "autosave").</param>
    /// <param name="label">Optional display label stored in the slot's metadata (e.g., "Chapter 3").</param>
    public void SetSlot(string slotName, string label = null) => _slotManager.SetSlot(slotName, label);

    /// <summary>
    /// Clears the active slot. File operations revert to <c>Application.persistentDataPath</c> directly.
    /// </summary>
    public void ClearSlot() => _slotManager.ClearSlot();

    /// <summary>
    /// Updates the display label stored in the active slot's metadata.
    /// </summary>
    /// <param name="label">The label to store (e.g., "Chapter 3 - The Forest").</param>
    /// <exception cref="System.InvalidOperationException">Thrown when no slot is active.</exception>
    public void SetSlotLabel(string label) => _slotManager.SetSlotLabel(label);

    /// <summary>
    /// Returns metadata for each slot name listed in <c>slots.registry.json</c> at the root of
    /// <c>Application.persistentDataPath</c>, in registry order. The registry is authoritative:
    /// extra directories without a registry entry are ignored. Slots without a <c>slot.meta</c> file
    /// return a <see cref="SaveSlotInfo"/> with only <see cref="SaveSlotInfo.Name"/> populated.
    /// </summary>
    public SaveSlotInfo[] ListSlots() => _slotManager.ListSlots();

    /// <summary>
    /// Deletes a slot directory and all save files within it.
    /// If the deleted slot is currently active, call <see cref="SetSlot"/> or <see cref="ClearSlot"/>
    /// before saving again.
    /// </summary>
    /// <param name="slotName">The slot name to delete.</param>
    public void DeleteSlot(string slotName)
    {
      try
      {
        _slotManager.DeleteSlot(slotName);
      }
      catch (System.Exception ex)
      {
        RaiseError("Slot deletion error: ", slotName, SaveOperation.Delete, ex);
      }
    }

    // -------------------------------------------------------------------------
    // Save
    // -------------------------------------------------------------------------

    /// <summary>
    /// Serializes and saves <paramref name="dataToStore"/> to disk.
    /// </summary>
    /// <param name="dataToStore">The object to save. Must be serializable by the active strategy.</param>
    /// <param name="dataName">File name without extension. Used to identify the save file.</param>
    public void SaveToFile<T>(T dataToStore, string dataName)
    {
      try
      {
        _strategy.Write(BuildFilePath(dataName), dataToStore);
        if (_slotManager.ActiveSlot != null) _slotManager.UpdateSlotMeta();
      }
      catch (System.Exception ex)
      {
        RaiseError("File writing error: ", dataName, SaveOperation.Save, ex);
      }
    }

    /// <summary>
    /// Serializes and saves <paramref name="dataToStore"/> wrapped in a versioned envelope.
    /// Use <see cref="LoadFromFile{T}(string,T,int)"/> to load — files saved with a version
    /// will be rejected on load if the version does not match.
    /// </summary>
    /// <param name="dataToStore">The object to save. Must be serializable by the active strategy.</param>
    /// <param name="dataName">File name without extension. Used to identify the save file.</param>
    /// <param name="version">Schema version to stamp on this save file.</param>
    public void SaveToFile<T>(T dataToStore, string dataName, int version)
    {
      try
      {
        _strategy.WriteVersioned(BuildFilePath(dataName), dataToStore, version);
        if (_slotManager.ActiveSlot != null) _slotManager.UpdateSlotMeta();
      }
      catch (System.Exception ex)
      {
        RaiseError("File writing error: ", dataName, SaveOperation.Save, ex);
      }
    }

    /// <summary>
    /// Async version of <see cref="SaveToFile{T}(T,string)"/>.
    /// </summary>
    /// <param name="dataToStore">The object to save. Must be serializable by the active strategy.</param>
    /// <param name="dataName">File name without extension. Used to identify the save file.</param>
    public async Task SaveToFileAsync<T>(T dataToStore, string dataName, CancellationToken ct = default)
    {
      try
      {
        await _strategy.WriteAsync(BuildFilePath(dataName), dataToStore, ct).ConfigureAwait(false);
        if (_slotManager.ActiveSlot != null) _slotManager.UpdateSlotMeta();
      }
      catch (OperationCanceledException)
      {
        throw;
      }
      catch (System.Exception ex)
      {
        RaiseError("File writing error: ", dataName, SaveOperation.Save, ex);
      }
    }

    /// <summary>
    /// Async version of <see cref="SaveToFile{T}(T,string,int)"/>.
    /// </summary>
    /// <param name="dataToStore">The object to save. Must be serializable by the active strategy.</param>
    /// <param name="dataName">File name without extension. Used to identify the save file.</param>
    /// <param name="version">Schema version to stamp on this save file.</param>
    public async Task SaveToFileAsync<T>(T dataToStore, string dataName, int version, CancellationToken ct = default)
    {
      try
      {
        await _strategy.WriteVersionedAsync(BuildFilePath(dataName), dataToStore, version, ct).ConfigureAwait(false);
        if (_slotManager.ActiveSlot != null) _slotManager.UpdateSlotMeta();
      }
      catch (OperationCanceledException)
      {
        throw;
      }
      catch (System.Exception ex)
      {
        RaiseError("File writing error: ", dataName, SaveOperation.Save, ex);
      }
    }

    // -------------------------------------------------------------------------
    // Load
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true if a save file with the given name exists on disk.
    /// Call this before loading to avoid triggering the error fallback for expected missing files.
    /// </summary>
    /// <param name="dataName">File name without extension.</param>
    public bool FileExists(string dataName) => File.Exists(BuildFilePath(dataName));

    /// <summary>
    /// Loads and deserializes a save file from disk.
    /// Returns <paramref name="defaultValue"/> if the file does not exist or cannot be read,
    /// and resets the file to <paramref name="defaultValue"/> so subsequent loads succeed.
    /// </summary>
    /// <param name="dataName">File name without extension.</param>
    /// <param name="defaultValue">Returned and written to disk when loading fails.</param>
    public T LoadFromFile<T>(string dataName, T defaultValue)
    {
      try
      {
        return _strategy.Read<T>(BuildFilePath(dataName));
      }
      catch (System.Exception ex)
      {
        RaiseError("File reading error: ", dataName, SaveOperation.Load, ex);
        ResetData(dataName, defaultValue);
        return defaultValue;
      }
    }

    /// <summary>
    /// Loads a versioned save file written by <see cref="SaveToFile{T}(T,string,int)"/>.
    /// Returns <paramref name="defaultValue"/> if the file's version does not match
    /// <paramref name="expectedVersion"/>, allowing callers to handle stale saves cleanly.
    /// </summary>
    /// <param name="dataName">File name without extension.</param>
    /// <param name="defaultValue">Returned when loading fails or the version does not match.</param>
    /// <param name="expectedVersion">The schema version this load call expects.</param>
    public T LoadFromFile<T>(string dataName, T defaultValue, int expectedVersion)
    {
      try
      {
        return _strategy.ReadVersioned<T>(BuildFilePath(dataName), expectedVersion);
      }
      catch (VersionMismatchException)
      {
        return defaultValue;
      }
      catch (System.Exception ex)
      {
        RaiseError("File reading error: ", dataName, SaveOperation.Load, ex);
        SaveToFile(defaultValue, dataName, expectedVersion);
        return defaultValue;
      }
    }

    /// <summary>
    /// Async version of <see cref="LoadFromFile{T}(string,T)"/>.
    /// </summary>
    /// <param name="dataName">File name without extension.</param>
    /// <param name="defaultValue">Returned and written to disk when loading fails.</param>
    public async Task<T> LoadFromFileAsync<T>(string dataName, T defaultValue, CancellationToken ct = default)
    {
      try
      {
        return await _strategy.ReadAsync<T>(BuildFilePath(dataName), ct).ConfigureAwait(false);
      }
      catch (OperationCanceledException)
      {
        throw;
      }
      catch (System.Exception ex)
      {
        RaiseError("File reading error: ", dataName, SaveOperation.Load, ex);
        await ResetDataAsync(dataName, defaultValue).ConfigureAwait(false);
        return defaultValue;
      }
    }

    /// <summary>
    /// Async version of <see cref="LoadFromFile{T}(string,T,int)"/>.
    /// </summary>
    /// <param name="dataName">File name without extension.</param>
    /// <param name="defaultValue">Returned when loading fails or the version does not match.</param>
    /// <param name="expectedVersion">The schema version this load call expects.</param>
    public async Task<T> LoadFromFileAsync<T>(string dataName, T defaultValue, int expectedVersion, CancellationToken ct = default)
    {
      try
      {
        return await _strategy.ReadVersionedAsync<T>(BuildFilePath(dataName), expectedVersion, ct).ConfigureAwait(false);
      }
      catch (VersionMismatchException)
      {
        return defaultValue;
      }
      catch (OperationCanceledException)
      {
        throw;
      }
      catch (System.Exception ex)
      {
        RaiseError("File reading error: ", dataName, SaveOperation.Load, ex);
        await ResetDataVersionedAsync(dataName, defaultValue, expectedVersion, ct).ConfigureAwait(false);
        return defaultValue;
      }
    }

    /// <summary>
    /// Loads and deserializes a save file bundled in the project's Resources folder.
    /// Useful for shipping default data with the game. The file must be a <c>TextAsset</c>.
    /// When using <see cref="StorageMethod.Encrypted"/>, the asset must have been encrypted
    /// with the same key provided to this SaveManager.
    /// If nothing is found at <paramref name="resourcePath"/>, <c>OnError</c> is notified and
    /// <c>default</c> is returned (no exception thrown).
    /// </summary>
    /// <param name="resourcePath">Path relative to a Resources folder, without extension.</param>
    public T LoadFromResources<T>(string resourcePath)
    {
      try
      {
        TextAsset asset = Resources.Load<TextAsset>(resourcePath);
        if (asset == null)
        {
          RaiseError(
            "Resource reading error: ",
            resourcePath,
            SaveOperation.Load,
            new System.InvalidOperationException(
              $"No TextAsset at Resources path '{resourcePath}'. Use a path under a Resources folder and omit the file extension."));
          return default;
        }
        return _strategy.ReadFromBytes<T>(asset.bytes);
      }
      catch (System.Exception ex)
      {
        RaiseError("Resource reading error: ", resourcePath, SaveOperation.Load, ex);
        return default;
      }
    }

    // -------------------------------------------------------------------------
    // Remove
    // -------------------------------------------------------------------------

    /// <summary>
    /// Deletes the active slot's directory and everything inside it. Requires an active slot — it will not
    /// delete <c>Application.persistentDataPath</c> when no slot is set (use <see cref="RemovePersistentDataRoot"/> for that).
    /// If you remove the active slot folder, call <see cref="SetSlot"/> or <see cref="ClearSlot"/> before saving again.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">Thrown when no slot is active.</exception>
    public void RemoveData()
    {
      if (_slotManager.ActiveSlot == null)
        throw new System.InvalidOperationException(
          "RemoveData() requires an active save slot. Call SetSlot(...) first, or use RemovePersistentDataRoot() only if you intend to delete the entire persistent data directory.");

      DeleteSlot(_slotManager.ActiveSlot);
    }

    /// <summary>
    /// Deletes <c>Application.persistentDataPath</c> recursively — all slots, the slot registry, and any
    /// other files stored under that root. This is intentionally separate from <see cref="RemoveData()"/>
    /// to avoid accidental wipes when no slot is active.
    /// </summary>
    public void RemovePersistentDataRoot()
    {
      string target = _slotManager.PersistentDataRoot;
      try
      {
        Directory.Delete(target, true);
      }
      catch (System.Exception ex)
      {
        RaiseError("Directory deletion error: ", target, SaveOperation.Delete, ex);
      }
    }

    /// <summary>
    /// Deletes a single save file by name.
    /// </summary>
    /// <param name="dataName">File name without extension.</param>
    public void RemoveData(string dataName)
    {
      string path = BuildFilePath(dataName);
      try
      {
        File.Delete(path);
        string backupPath = path + ".bak";
        if (File.Exists(backupPath))
          File.Delete(backupPath);
      }
      catch (System.Exception ex)
      {
        RaiseError("File deletion error: ", dataName, SaveOperation.Delete, ex);
      }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private string BuildFilePath(string dataName) =>
      Path.Combine(_slotManager.GetSaveDirectory(), dataName + "." + _strategy.FileExtension);

    private void ResetData<T>(string dataName, T defaultValue) => SaveToFile(defaultValue, dataName);

    private async Task ResetDataAsync<T>(string dataName, T defaultValue) =>
      await SaveToFileAsync(defaultValue, dataName).ConfigureAwait(false);

    private async Task ResetDataVersionedAsync<T>(string dataName, T defaultValue, int expectedVersion, CancellationToken ct) =>
      await SaveToFileAsync(defaultValue, dataName, expectedVersion, ct).ConfigureAwait(false);

    private void RaiseError(string message, string dataName, SaveOperation operation, System.Exception ex)
    {
      SaveManagerException saveEx = new SaveManagerException(message + ex.Message, dataName, operation, ex);
      Debug.LogWarning(saveEx.Message);
      OnError?.Invoke(saveEx);
    }
  }
}

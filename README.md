# Save Management System
**Save Management System** used in our projects. Handles persistent game data with multi-slot support, versioned saves, and optional encryption.

## 1. Getting Started

1. Open up project packages manifest JSON: [Project Folder]/Packages/manifest.json
2. Add following line on top of **dependencies**: `"net.dynamicbox.savemanagement": "https://github.com/mechaniqe/save-management.git",`

The end result should be similar to:
```
{
  "dependencies": {
    "net.dynamicbox.savemanagement": "https://github.com/mechaniqe/save-management.git",
    ...
  }
}
```

## 2. How to Use

SaveManager is the single entry point for all read and write operations. Create an instance, optionally activate a slot, and start saving. Subscribe to `OnError` to handle failures without wrapping every call in try/catch.

### 2.1 Creating a SaveManager

Pick a storage format and create an instance. JSON is the default. XML is human-readable and format-agnostic. Encrypted wraps JSON with AES-256 — keep your key consistent between saves and loads or you will have a bad time.

```csharp
using DynamicBox.SaveManagement;

// JSON (default)
var saveManager = new SaveManager(StorageMethod.JSON);

// XML
var saveManager = new SaveManager(StorageMethod.XML);

// Encrypted
var saveManager = new SaveManager(StorageMethod.Encrypted, "your-secret-key");
```

You can also bring your own serializer by implementing `IStorageStrategy` and passing it directly:

```csharp
var saveManager = new SaveManager(new MyCustomStrategy());
```

### 2.2 Saving data

Call `SaveToFile` with your data object and a file name. The name is used as the file name on disk — no extension needed.

```csharp
using DynamicBox.SaveManagement;

[System.Serializable]
public class PlayerData
{
  public int level;
  public float health;
}

var player = new PlayerData { level = 5, health = 100f };
saveManager.SaveToFile(player, "player");
```

Need to guard against loading stale saves after a data model change? Stamp a version number:

```csharp
saveManager.SaveToFile(player, "player", version: 2);
```

An async overload is available for larger saves:

```csharp
await saveManager.SaveToFileAsync(player, "player");
```

### 2.3 Loading data

Pass a default value — it is returned (and written to disk) if the file is missing or unreadable, so subsequent loads stay consistent.

```csharp
var player = saveManager.LoadFromFile<PlayerData>("player", new PlayerData());
```

Loading a versioned save returns the default if the version does not match, without triggering `OnError`. Handy for detecting when a player's save is outdated.

```csharp
var player = saveManager.LoadFromFile<PlayerData>("player", new PlayerData(), expectedVersion: 2);
```

You can also load from a bundled Resources asset — useful for shipping default game state with the build:

```csharp
var defaults = saveManager.LoadFromResources<PlayerData>("Defaults/player");
```

### 2.4 Using save slots

Slots are subdirectories under `Application.persistentDataPath`. Activate one before saving and all reads/writes go there automatically. Great for multiple save files or autosave separation.

```csharp
// Activate a slot (creates the directory if it does not exist)
saveManager.SetSlot("slot_1", label: "Chapter 3 - The Forest");

// All saves/loads now go to slot_1/
saveManager.SaveToFile(player, "player");

// List registered slots for a save-select screen (ordered; see slots.registry.json)
SaveSlotInfo[] slots = saveManager.ListSlots();
foreach (var slot in slots)
  Debug.Log($"{slot.Name} — {slot.Label} — {slot.LastModified}");

// Delete a slot and everything in it
saveManager.DeleteSlot("slot_1");

// Revert to the root directory
saveManager.ClearSlot();
```

### 2.5 Handling errors

Subscribe to `OnError` once and handle failures in one place. The event provides the file name, operation type, and the original exception.

```csharp
using DynamicBox.SaveManagement;

saveManager.OnError += OnSaveError;

private void OnSaveError(SaveManagerException ex)
{
  Debug.LogError($"[{ex.Operation}] {ex.DataName}: {ex.Message}");
  // show a UI warning, report to analytics, etc.
}
```

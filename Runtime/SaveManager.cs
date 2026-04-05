using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Xml.Serialization;
using UnityEngine;

namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// Handles reading and writing persistent save data using Binary, XML, or JSON serialization.
  /// All files are stored in <c>Application.persistentDataPath</c>.
  /// Subscribe to <see cref="OnError"/> to handle failures without polling return values.
  /// </summary>
  public class SaveManager
  {
    private string _savingLocation;
    private StorageMethod _method;

    /// <summary>
    /// Fired whenever a save, load, or delete operation fails.
    /// Provides the file name, operation type, and original exception.
    /// </summary>
    public event System.Action<SaveManagerException> OnError;

    /// <summary>
    /// Creates a new SaveManager using the specified serialization format.
    /// </summary>
    /// <param name="method">The format used for all read and write operations.</param>
    public SaveManager(StorageMethod method)
    {
      _savingLocation = Application.persistentDataPath;
      _method = method;
    }

    // -------------------------------------------------------------------------
    // Save
    // -------------------------------------------------------------------------

    /// <summary>
    /// Serializes and saves <paramref name="dataToStore"/> to disk.
    /// </summary>
    /// <param name="dataToStore">The object to save. Must be serializable in the chosen format.</param>
    /// <param name="dataName">File name without extension. Used to identify the save file.</param>
    public void SaveToFile<T>(T dataToStore, string dataName)
    {
      string fileName = _savingLocation + "/" + dataName + "." + _method.ToString().ToLower();

      try
      {
        switch (_method)
        {
          case StorageMethod.Binary:
            BinaryFormatter formatter = new BinaryFormatter();
            using (FileStream stream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
              formatter.Serialize(stream, dataToStore);
            }
            break;

          case StorageMethod.XML:
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (FileStream stream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
              serializer.Serialize(stream, dataToStore);
            }
            break;

          case StorageMethod.JSON:
            string serializedData = JsonUtility.ToJson(dataToStore, true);
            File.WriteAllText(fileName, serializedData);
            break;
        }
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
    /// <param name="dataToStore">The object to save. Must be serializable in the chosen format.</param>
    /// <param name="dataName">File name without extension. Used to identify the save file.</param>
    /// <param name="version">Schema version to stamp on this save file.</param>
    public void SaveToFile<T>(T dataToStore, string dataName, int version)
    {
      string fileName = _savingLocation + "/" + dataName + "." + _method.ToString().ToLower();

      try
      {
        switch (_method)
        {
          case StorageMethod.Binary:
            BinaryFormatter formatter = new BinaryFormatter();
            using (FileStream stream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
              formatter.Serialize(stream, new SaveEnvelope<T> { Version = version, Data = dataToStore });
            }
            break;

          case StorageMethod.XML:
            XmlSerializer serializer = new XmlSerializer(typeof(SaveEnvelope<T>));
            using (FileStream stream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
              serializer.Serialize(stream, new SaveEnvelope<T> { Version = version, Data = dataToStore });
            }
            break;

          case StorageMethod.JSON:
            JsonEnvelope envelope = new JsonEnvelope
            {
              version = version,
              data = JsonUtility.ToJson(dataToStore, true)
            };
            File.WriteAllText(fileName, JsonUtility.ToJson(envelope, true));
            break;
        }
      }
      catch (System.Exception ex)
      {
        RaiseError("File writing error: ", dataName, SaveOperation.Save, ex);
      }
    }

    /// <summary>
    /// Async version of <see cref="SaveToFile{T}(T,string)"/>. File I/O runs off the main thread.
    /// Binary and XML serialization also runs off the main thread. JSON serialization remains
    /// on the calling thread due to a Unity restriction on <c>JsonUtility</c>.
    /// </summary>
    /// <param name="dataToStore">The object to save. Must be serializable in the chosen format.</param>
    /// <param name="dataName">File name without extension. Used to identify the save file.</param>
    public async Task SaveToFileAsync<T>(T dataToStore, string dataName)
    {
      string fileName = _savingLocation + "/" + dataName + "." + _method.ToString().ToLower();

      try
      {
        switch (_method)
        {
          case StorageMethod.Binary:
            await Task.Run(() =>
            {
              BinaryFormatter formatter = new BinaryFormatter();
              using (FileStream stream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
              {
                formatter.Serialize(stream, dataToStore);
              }
            });
            break;

          case StorageMethod.XML:
            await Task.Run(() =>
            {
              XmlSerializer serializer = new XmlSerializer(typeof(T));
              using (FileStream stream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
              {
                serializer.Serialize(stream, dataToStore);
              }
            });
            break;

          case StorageMethod.JSON:
            string serializedData = JsonUtility.ToJson(dataToStore, true);
            using (StreamWriter writer = new StreamWriter(fileName, false))
            {
              await writer.WriteAsync(serializedData);
            }
            break;
        }
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
    /// <param name="dataName">File name without extension, matching what was used in <see cref="SaveToFile{T}(T,string)"/>.</param>
    public bool FileExists(string dataName)
    {
      string fileName = _savingLocation + "/" + dataName + "." + _method.ToString().ToLower();

      return File.Exists(fileName);
    }

    /// <summary>
    /// Loads and deserializes a save file from disk.
    /// Returns <paramref name="defaultValue"/> if the file does not exist or cannot be read,
    /// and resets the file to <paramref name="defaultValue"/> so subsequent loads succeed.
    /// </summary>
    /// <param name="dataName">File name without extension, matching what was used in <see cref="SaveToFile{T}(T,string)"/>.</param>
    /// <param name="defaultValue">Returned and written to disk when loading fails.</param>
    public T LoadFromFile<T>(string dataName, T defaultValue)
    {
      T storedData = defaultValue;

      string fileName = _savingLocation + "/" + dataName + "." + _method.ToString().ToLower();

      try
      {
        switch (_method)
        {
          case StorageMethod.Binary:
            using (Stream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
              BinaryFormatter formatter = new BinaryFormatter();
              storedData = (T) formatter.Deserialize(stream);
            }
            break;

          case StorageMethod.XML:
            using (FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
              XmlSerializer serializer = new XmlSerializer(typeof(T));
              storedData = (T) serializer.Deserialize(stream);
            }
            break;

          case StorageMethod.JSON:
            string serializedData = File.ReadAllText(fileName);
            storedData = JsonUtility.FromJson<T>(serializedData);
            break;
        }
      }
      catch (System.Exception ex)
      {
        RaiseError("File reading error: ", dataName, SaveOperation.Load, ex);
        ResetData<T>(dataName, defaultValue);
        storedData = defaultValue;
      }

      return storedData;
    }

    /// <summary>
    /// Loads a versioned save file written by <see cref="SaveToFile{T}(T,string,int)"/>.
    /// Returns <paramref name="defaultValue"/> if the file's version does not match
    /// <paramref name="expectedVersion"/>, allowing callers to handle stale saves cleanly.
    /// </summary>
    /// <param name="dataName">File name without extension, matching what was used in <see cref="SaveToFile{T}(T,string,int)"/>.</param>
    /// <param name="defaultValue">Returned when loading fails or the version does not match.</param>
    /// <param name="expectedVersion">The schema version this load call expects.</param>
    public T LoadFromFile<T>(string dataName, T defaultValue, int expectedVersion)
    {
      string fileName = _savingLocation + "/" + dataName + "." + _method.ToString().ToLower();

      try
      {
        switch (_method)
        {
          case StorageMethod.Binary:
            using (Stream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
              BinaryFormatter formatter = new BinaryFormatter();
              SaveEnvelope<T> envelope = (SaveEnvelope<T>) formatter.Deserialize(stream);
              return envelope.Version == expectedVersion ? envelope.Data : defaultValue;
            }

          case StorageMethod.XML:
            using (FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
              XmlSerializer serializer = new XmlSerializer(typeof(SaveEnvelope<T>));
              SaveEnvelope<T> envelope = (SaveEnvelope<T>) serializer.Deserialize(stream);
              return envelope.Version == expectedVersion ? envelope.Data : defaultValue;
            }

          case StorageMethod.JSON:
            string raw = File.ReadAllText(fileName);
            JsonEnvelope jsonEnvelope = JsonUtility.FromJson<JsonEnvelope>(raw);
            if (jsonEnvelope.version != expectedVersion)
              return defaultValue;
            return JsonUtility.FromJson<T>(jsonEnvelope.data);
        }
      }
      catch (System.Exception ex)
      {
        RaiseError("File reading error: ", dataName, SaveOperation.Load, ex);
        SaveToFile(defaultValue, dataName, expectedVersion);
        return defaultValue;
      }

      return defaultValue;
    }

    /// <summary>
    /// Async version of <see cref="LoadFromFile{T}(string,T)"/>. File I/O runs off the main thread.
    /// JSON deserialization remains on the calling thread due to a Unity restriction on <c>JsonUtility</c>.
    /// </summary>
    /// <param name="dataName">File name without extension, matching what was used in <see cref="SaveToFileAsync{T}"/>.</param>
    /// <param name="defaultValue">Returned and written to disk when loading fails.</param>
    public async Task<T> LoadFromFileAsync<T>(string dataName, T defaultValue)
    {
      string fileName = _savingLocation + "/" + dataName + "." + _method.ToString().ToLower();

      try
      {
        switch (_method)
        {
          case StorageMethod.Binary:
            return await Task.Run(() =>
            {
              using (Stream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
              {
                BinaryFormatter formatter = new BinaryFormatter();
                return (T) formatter.Deserialize(stream);
              }
            });

          case StorageMethod.XML:
            return await Task.Run(() =>
            {
              using (FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
              {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                return (T) serializer.Deserialize(stream);
              }
            });

          case StorageMethod.JSON:
            string serializedData;
            using (StreamReader reader = new StreamReader(fileName))
            {
              serializedData = await reader.ReadToEndAsync();
            }
            return JsonUtility.FromJson<T>(serializedData);
        }
      }
      catch (System.Exception ex)
      {
        RaiseError("File reading error: ", dataName, SaveOperation.Load, ex);
        await ResetDataAsync<T>(dataName, defaultValue);
        return defaultValue;
      }

      return defaultValue;
    }

    /// <summary>
    /// Loads and deserializes a save file bundled in the project's Resources folder.
    /// Useful for shipping default data with the game. The file must be a <c>TextAsset</c>.
    /// </summary>
    /// <param name="resourcePath">Path relative to a Resources folder, without extension.</param>
    public T LoadFromResources<T>(string resourcePath)
    {
      T storedData = default(T);

      try
      {
        TextAsset textAsset = Resources.Load<TextAsset>(resourcePath);

        switch (_method)
        {
          case StorageMethod.Binary:
            using (Stream stream = new MemoryStream(textAsset.bytes))
            {
              BinaryFormatter formatter = new BinaryFormatter();
              storedData = (T) formatter.Deserialize(stream);
            }
            break;

          case StorageMethod.XML:
            using (Stream stream = new MemoryStream(textAsset.bytes))
            {
              XmlSerializer serializer = new XmlSerializer(typeof(T));
              storedData = (T) serializer.Deserialize(stream);
            }
            break;

          case StorageMethod.JSON:
            storedData = JsonUtility.FromJson<T>(textAsset.text);
            break;
        }
      }
      catch (System.Exception ex)
      {
        RaiseError("Resource reading error: ", resourcePath, SaveOperation.Load, ex);
      }

      return storedData;
    }

    // -------------------------------------------------------------------------
    // Remove
    // -------------------------------------------------------------------------

    /// <summary>
    /// Deletes the entire persistent data directory. All save files will be lost.
    /// </summary>
    public void RemoveData()
    {
      try
      {
        Directory.Delete(_savingLocation, true);
      }
      catch (System.Exception ex)
      {
        RaiseError("Directory deletion error: ", _savingLocation, SaveOperation.Delete, ex);
      }
    }

    /// <summary>
    /// Deletes a single save file by name.
    /// </summary>
    /// <param name="dataName">File name without extension, matching what was used in <see cref="SaveToFile{T}(T,string)"/>.</param>
    public void RemoveData(string dataName)
    {
      string fileName = _savingLocation + "/" + dataName + "." + _method.ToString().ToLower();

      try
      {
        File.Delete(fileName);
      }
      catch (System.Exception ex)
      {
        RaiseError("File deletion error: ", dataName, SaveOperation.Delete, ex);
      }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void ResetData<T>(string dataName, T defaultValue)
    {
      SaveToFile(defaultValue, dataName);
    }

    private async Task ResetDataAsync<T>(string dataName, T defaultValue)
    {
      await SaveToFileAsync(defaultValue, dataName);
    }

    private void RaiseError(string message, string dataName, SaveOperation operation, System.Exception ex)
    {
      SaveManagerException saveEx = new SaveManagerException(message + ex.Message, dataName, operation, ex);
      Debug.LogWarning(saveEx.Message);
      OnError?.Invoke(saveEx);
    }

    // Used internally for JSON versioning. JsonUtility cannot serialize open
    // generic types, so T is serialized separately and stored as a string.
    [System.Serializable]
    private class JsonEnvelope
    {
      public int version;
      public string data;
    }
  }
}

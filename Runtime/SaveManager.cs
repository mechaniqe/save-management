using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;
using UnityEngine;

namespace DynamicBox.SaveManagement
{
    public class SaveManager
    {
        private string _savingLocation;
        private StorageMethod _method;

        public SaveManager (StorageMethod method)
        {
            _savingLocation = Application.persistentDataPath;
            _method = method;
        }

        public void SaveToFile<T> (T dataToStore, string dataName)
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
                Debug.LogWarning("File writing error: " + ex.Message);
            }
        }

        public bool FileExists (string dataName)
        {
            string fileName = _savingLocation + "/" + dataName + "." + _method.ToString ().ToLower ();

            return File.Exists (fileName);
        }

        public T LoadFromFile<T> (string dataName, T defaultValue)
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
                            storedData = (T)formatter.Deserialize(stream);

                            stream.Close();
                            stream.Dispose();
                        }

                        break;
                    case StorageMethod.XML:
                        using (FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            XmlSerializer serializer = new XmlSerializer(typeof(T));
                            storedData = (T)serializer.Deserialize(stream);

                            stream.Close();
                            stream.Dispose();
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
                Debug.LogWarning ("File reading error: " + ex.Message);
                
                ResetData<T> (dataName, defaultValue);
                storedData = LoadFromFile<T> (dataName, defaultValue);
            }

            return storedData;
        }

        public T LoadFromResources<T> (string resourcePath)
        {
            T storedData = default (T);

            try
            {
                TextAsset textAsset = Resources.Load<TextAsset>(resourcePath);

                switch (_method)
                {
                    case StorageMethod.Binary:
                        using (Stream stream = new MemoryStream (textAsset.bytes))
                        {
                            BinaryFormatter formatter = new BinaryFormatter ();
                            storedData = (T) formatter.Deserialize (stream);

                            stream.Close ();
                            stream.Dispose ();
                        }

                        break;
                    case StorageMethod.XML:
                        using (Stream stream = new MemoryStream (textAsset.bytes))
                        {
                            XmlSerializer serializer = new XmlSerializer (typeof (T));
                            storedData = (T) serializer.Deserialize (stream);

                            stream.Close ();
                            stream.Dispose ();
                        }

                        break;
                    case StorageMethod.JSON:
                        storedData = JsonUtility.FromJson<T> (textAsset.text);

                        break;
                }
            }
            catch (System.Exception ex)
            {

                Debug.LogWarning ("File reading error: " + ex.Message);
            }

            return storedData;
        }

        private void ResetData<T> (string dataName, T defaultValue)
        {
            T initialValue = defaultValue;

            SaveToFile (initialValue, dataName);
        }

        public void RemoveData()
        {
            for (int i = 0; i < System.Enum.GetNames(typeof(StorageMethod)).Length; i++)
            {
                string fileName = _savingLocation + "." + ((StorageMethod)i).ToString().ToLower();

                try
                {
                    File.Delete(fileName);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning(fileName + " deleting error: " + ex.Message);
                }
            }
        }
    }
}
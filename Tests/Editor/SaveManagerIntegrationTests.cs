using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace DynamicBox.SaveManagement.Tests.Editor
{
  /// <summary>
  /// Integration tests using an isolated temp directory as <see cref="SaveManager"/> root
  /// (via <c>persistentDataRoot</c>). Run in Unity Test Runner (Edit Mode).
  /// </summary>
  [TestFixture]
  public class SaveManagerIntegrationTests
  {
    private string _tempRoot;

    [SetUp]
    public void SetUp()
    {
      _tempRoot = Path.Combine(Path.GetTempPath(), "SaveManagementTests_" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void TearDown()
    {
      try
      {
        if (Directory.Exists(_tempRoot))
          Directory.Delete(_tempRoot, true);
      }
      catch (IOException)
      {
        // Best-effort cleanup; temp path is unique per run.
      }
    }

    [Test]
    public void Json_SaveAndLoad_RoundTrips()
    {
      var mgr = new SaveManager(StorageMethod.JSON, persistentDataRoot: _tempRoot);
      var written = new JsonPayload { Value = 42 };
      mgr.SaveToFile(written, "save1");
      JsonPayload read = mgr.LoadFromFile("save1", new JsonPayload());
      Assert.AreEqual(42, read.Value);
    }

    [Test]
    public void Json_Versioned_Load_ReturnsDefault_WhenVersionMismatch()
    {
      var mgr = new SaveManager(StorageMethod.JSON, persistentDataRoot: _tempRoot);
      mgr.SaveToFile(new JsonPayload { Value = 1 }, "v", version: 1);
      int errors = 0;
      mgr.OnError += _ => errors++;
      JsonPayload read = mgr.LoadFromFile("v", new JsonPayload { Value = 99 }, expectedVersion: 2);
      Assert.AreEqual(99, read.Value);
      Assert.AreEqual(0, errors);
    }

    [Test]
    public void Json_Versioned_SaveAndLoad_MatchesExpectedVersion()
    {
      var mgr = new SaveManager(StorageMethod.JSON, persistentDataRoot: _tempRoot);
      mgr.SaveToFile(new JsonPayload { Value = 7 }, "v2", version: 3);
      JsonPayload read = mgr.LoadFromFile("v2", new JsonPayload(), expectedVersion: 3);
      Assert.AreEqual(7, read.Value);
    }

    [Test]
    public void Json_SetSlot_Save_ListSlots_ContainsSlot()
    {
      var mgr = new SaveManager(StorageMethod.JSON, persistentDataRoot: _tempRoot);
      mgr.SetSlot("slot_a", label: "L1");
      mgr.SaveToFile(new JsonPayload { Value = 1 }, "game");
      SaveSlotInfo[] slots = mgr.ListSlots();
      Assert.AreEqual(1, slots.Length);
      Assert.AreEqual("slot_a", slots[0].Name);
      Assert.AreEqual("L1", slots[0].Label);
    }

    [Test]
    public void Encrypted_SaveAndLoad_RoundTrips()
    {
      var mgr = new SaveManager(StorageMethod.Encrypted, "test-key", persistentDataRoot: _tempRoot);
      mgr.SaveToFile(new JsonPayload { Value = 100 }, "enc");
      JsonPayload read = mgr.LoadFromFile("enc", new JsonPayload());
      Assert.AreEqual(100, read.Value);
    }

    [Test]
    public void Xml_SaveAndLoad_RoundTrips()
    {
      var mgr = new SaveManager(StorageMethod.XML, persistentDataRoot: _tempRoot);
      mgr.SaveToFile(new XmlPayload { Value = 3 }, "xmlsave");
      XmlPayload read = mgr.LoadFromFile("xmlsave", new XmlPayload());
      Assert.AreEqual(3, read.Value);
    }

    [Test]
    public void Json_AsyncSaveAndLoad_RoundTrips()
    {
      var mgr = new SaveManager(StorageMethod.JSON, persistentDataRoot: _tempRoot);
      SaveToFileAsyncSync(mgr, new JsonPayload { Value = 5 }, "async");
      JsonPayload read = LoadFromFileAsyncSync(mgr, "async", new JsonPayload());
      Assert.AreEqual(5, read.Value);
    }

    [Test]
    public void Json_AsyncVersioned_RoundTrips()
    {
      var mgr = new SaveManager(StorageMethod.JSON, persistentDataRoot: _tempRoot);
      SaveToFileAsyncSync(mgr, new JsonPayload { Value = 8 }, "av", version: 4);
      JsonPayload read = LoadFromFileAsyncSync(mgr, "av", new JsonPayload(), expectedVersion: 4);
      Assert.AreEqual(8, read.Value);
    }

    private static void SaveToFileAsyncSync(SaveManager mgr, JsonPayload data, string name, int? version = null)
    {
      Task task = version.HasValue
        ? mgr.SaveToFileAsync(data, name, version.Value)
        : mgr.SaveToFileAsync(data, name);
      task.GetAwaiter().GetResult();
    }

    private static JsonPayload LoadFromFileAsyncSync(SaveManager mgr, string name, JsonPayload def, int? expectedVersion = null)
    {
      Task<JsonPayload> task = expectedVersion.HasValue
        ? mgr.LoadFromFileAsync(name, def, expectedVersion.Value)
        : mgr.LoadFromFileAsync(name, def);
      return task.GetAwaiter().GetResult();
    }

    [Serializable]
    private class JsonPayload
    {
      public int Value;
    }

    /// <summary>XmlSerializer-friendly payload (public properties).</summary>
    private class XmlPayload
    {
      public int Value { get; set; }
    }
  }
}

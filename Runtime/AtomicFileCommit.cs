using System.IO;

namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// Promotes a completed temp file at <c>targetPath + ".tmp"</c> to <paramref name="targetPath"/>,
  /// rotating any existing file to <c>targetPath + ".bak"</c> first.
  /// </summary>
  internal static class AtomicFileCommit
  {
    internal static void Apply(string targetPath)
    {
      string tempPath = targetPath + ".tmp";
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

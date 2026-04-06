using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicBox.SaveManagement
{
  /// <summary>
  /// <see cref="IStorageStrategy"/> implementation that serializes data as JSON and encrypts
  /// it with AES-256. The encryption key is derived from an arbitrary string via SHA-256.
  /// </summary>
  public class EncryptedStorageStrategy : JsonStorageStrategyBase
  {
    private readonly string _encryptionKey;

    /// <param name="encryptionKey">
    /// Any string. Hashed to a fixed key size internally.
    /// Must remain consistent between saves and loads — changing it makes existing saves unreadable.
    /// </param>
    /// <param name="jsonSerializer">
    /// Serializer for JSON before encryption. If null, <see cref="SaveManager.JsonSerializer"/> is used.
    /// </param>
    public EncryptedStorageStrategy(string encryptionKey, IJsonSerializer jsonSerializer = null)
      : base(jsonSerializer ?? SaveManager.JsonSerializer)
    {
      if (string.IsNullOrEmpty(encryptionKey))
        throw new System.ArgumentException("Encryption key cannot be null or empty.", nameof(encryptionKey));
      _encryptionKey = encryptionKey;
    }

    /// <inheritdoc/>
    public override string FileExtension => "encrypted";

    /// <inheritdoc cref="JsonStorageStrategyBase.WriteJsonToTemp"/>
    protected override void WriteJsonToTemp(string path, string json) =>
      File.WriteAllBytes(path + ".tmp", Encrypt(json));

    /// <inheritdoc cref="JsonStorageStrategyBase.ReadJsonFromFile"/>
    protected override string ReadJsonFromFile(string path) =>
      Decrypt(File.ReadAllBytes(path));

    /// <inheritdoc cref="JsonStorageStrategyBase.WriteJsonToTempAsync"/>
    protected override async Task WriteJsonToTempAsync(string path, string json, CancellationToken ct)
    {
      byte[] encryptedBytes = await Task.Run(() => Encrypt(json), ct).ConfigureAwait(false);
      using (FileStream fs = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
      {
        await fs.WriteAsync(encryptedBytes, 0, encryptedBytes.Length, ct).ConfigureAwait(false);
      }
    }

    /// <inheritdoc cref="JsonStorageStrategyBase.ReadJsonFromFileAsync"/>
    protected override async Task<string> ReadJsonFromFileAsync(string path, CancellationToken ct)
    {
      byte[] fileBytes;
      using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
      {
        fileBytes = new byte[fs.Length];
        await fs.ReadAsync(fileBytes, 0, fileBytes.Length, ct).ConfigureAwait(false);
      }
      return await Task.Run(() => Decrypt(fileBytes), ct).ConfigureAwait(false);
    }

    /// <inheritdoc cref="JsonStorageStrategyBase.ReadJsonFromBytes"/>
    protected override string ReadJsonFromBytes(byte[] rawBytes) => Decrypt(rawBytes);

    private byte[] Encrypt(string plainText)
    {
      using (Aes aes = Aes.Create())
      {
        aes.Key = DeriveKey(_encryptionKey);
        aes.GenerateIV();
        using (MemoryStream ms = new MemoryStream())
        {
          ms.Write(aes.IV, 0, aes.IV.Length);
          using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
          using (StreamWriter sw = new StreamWriter(cs))
          {
            sw.Write(plainText);
          }
          return ms.ToArray();
        }
      }
    }

    private string Decrypt(byte[] cipherData)
    {
      using (Aes aes = Aes.Create())
      {
        aes.Key = DeriveKey(_encryptionKey);
        byte[] iv = new byte[16];
        System.Array.Copy(cipherData, 0, iv, 0, 16);
        aes.IV = iv;
        using (MemoryStream ms = new MemoryStream(cipherData, 16, cipherData.Length - 16))
        using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
        using (StreamReader sr = new StreamReader(cs))
        {
          return sr.ReadToEnd();
        }
      }
    }

    private byte[] DeriveKey(string key)
    {
      using (SHA256 sha = SHA256.Create())
      {
        return sha.ComputeHash(Encoding.UTF8.GetBytes(key));
      }
    }
  }
}

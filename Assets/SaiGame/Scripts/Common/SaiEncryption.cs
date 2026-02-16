using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SaiGame.Services
{
    public static class SaiEncryption
    {
        private const string ENCRYPTION_PASSPHRASE = "SaiGame2026SecureKeyForEncryption";

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                byte[] key = new byte[32];
                byte[] iv = new byte[16];
                
                byte[] passphraseBytes = Encoding.UTF8.GetBytes(ENCRYPTION_PASSPHRASE);
                Array.Copy(passphraseBytes, key, Math.Min(passphraseBytes.Length, 32));
                Array.Copy(passphraseBytes, iv, Math.Min(passphraseBytes.Length, 16));

                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                    byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                    byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                    return Convert.ToBase64String(encryptedBytes);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Encryption error: {e.Message}");
                return string.Empty;
            }
        }

        public static string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return string.Empty;

            try
            {
                byte[] key = new byte[32];
                byte[] iv = new byte[16];
                
                byte[] passphraseBytes = Encoding.UTF8.GetBytes(ENCRYPTION_PASSPHRASE);
                Array.Copy(passphraseBytes, key, Math.Min(passphraseBytes.Length, 32));
                Array.Copy(passphraseBytes, iv, Math.Min(passphraseBytes.Length, 16));

                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                    byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

                    return Encoding.UTF8.GetString(decryptedBytes);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Decryption error: {e.Message}");
                return string.Empty;
            }
        }
    }
}

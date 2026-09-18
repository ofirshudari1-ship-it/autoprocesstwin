using System;
using System.Security.Cryptography;
using System.Text;

namespace AutoProcessTwin
{
    // Encrypts/decrypts sensitive values (API keys) with Windows DPAPI
    // (CurrentUser scope) so the value on disk is useless to another user
    // or to a process that reads config without running as this user.
    // Migration: if a stored value is not valid base64 or decryption fails,
    // falls back to treating it as plain text (supports upgrading from older
    // versions that stored keys in clear text).
    public static class CryptoHelper
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AutoProcessTwin-v1");

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return "";
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(plainText);
                byte[] enc = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(enc);
            }
            catch
            {
                return plainText;
            }
        }

        public static string Decrypt(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return "";
            try
            {
                byte[] enc = Convert.FromBase64String(stored);
                byte[] data = ProtectedData.Unprotect(enc, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                // Not DPAPI-encrypted (plain text from older version) — use as-is
                return stored;
            }
        }
    }
}

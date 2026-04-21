namespace Tempo.Core.Security
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// Encrypts and decrypts <see cref="AuthenticationToken"/> payloads using AES-256-CBC with a random IV.
    /// The produced tokens are Base64-URL strings that embed <c>iv|ciphertext</c>.
    /// </summary>
    public class TokenCipher
    {
        private readonly byte[] _Key;
        private readonly JsonSerializerOptions _JsonOptions;

        /// <summary>
        /// Instantiate the cipher.
        /// </summary>
        /// <param name="signingKey">
        /// Base signing key. If the UTF-8 byte length of the string is exactly 32, those bytes are used as the key.
        /// Otherwise the key is derived as <c>SHA-256(signingKey)</c>.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="signingKey"/> is null or empty.</exception>
        public TokenCipher(string signingKey)
        {
            if (String.IsNullOrEmpty(signingKey)) throw new ArgumentNullException(nameof(signingKey));
            byte[] raw = Encoding.UTF8.GetBytes(signingKey);
            _Key = raw.Length == 32 ? raw : SHA256.HashData(raw);
            _JsonOptions = new JsonSerializerOptions { WriteIndented = false };
        }

        /// <summary>
        /// Encrypt a token.
        /// </summary>
        /// <param name="token">Token to encrypt.</param>
        /// <returns>Base64-URL encoded token string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="token"/> is null.</exception>
        public string Encrypt(AuthenticationToken token)
        {
            if (token == null) throw new ArgumentNullException(nameof(token));
            string json = JsonSerializer.Serialize(token, _JsonOptions);
            byte[] plaintext = Encoding.UTF8.GetBytes(json);

            using (Aes aes = Aes.Create())
            {
                aes.Key = _Key;
                aes.GenerateIV();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (MemoryStream ms = new MemoryStream())
                {
                    ms.Write(aes.IV, 0, aes.IV.Length);
                    using (ICryptoTransform encryptor = aes.CreateEncryptor())
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(plaintext, 0, plaintext.Length);
                        cs.FlushFinalBlock();
                    }
                    return Base64UrlEncode(ms.ToArray());
                }
            }
        }

        /// <summary>
        /// Decrypt a token string.
        /// </summary>
        /// <param name="tokenString">Encrypted token.</param>
        /// <returns>Decrypted <see cref="AuthenticationToken"/>, or null when the token cannot be parsed.</returns>
        public AuthenticationToken? Decrypt(string tokenString)
        {
            if (String.IsNullOrEmpty(tokenString)) return null;
            try
            {
                byte[] payload = Base64UrlDecode(tokenString);
                if (payload.Length < 17) return null;

                byte[] iv = new byte[16];
                Buffer.BlockCopy(payload, 0, iv, 0, 16);
                int cipherLen = payload.Length - 16;
                byte[] cipher = new byte[cipherLen];
                Buffer.BlockCopy(payload, 16, cipher, 0, cipherLen);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = _Key;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    using (MemoryStream ms = new MemoryStream(cipher))
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (MemoryStream output = new MemoryStream())
                    {
                        cs.CopyTo(output);
                        string json = Encoding.UTF8.GetString(output.ToArray());
                        return JsonSerializer.Deserialize<AuthenticationToken>(json, _JsonOptions);
                    }
                }
            }
            catch (FormatException)
            {
                return null;
            }
            catch (CryptographicException)
            {
                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string Base64UrlEncode(byte[] data)
        {
            string b64 = Convert.ToBase64String(data);
            return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private static byte[] Base64UrlDecode(string value)
        {
            string s = value.Replace('-', '+').Replace('_', '/');
            int pad = s.Length % 4;
            if (pad > 0) s = s.PadRight(s.Length + (4 - pad), '=');
            return Convert.FromBase64String(s);
        }
    }
}

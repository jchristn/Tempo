namespace Tempo.Core.Security
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// SHA-256 password hashing helper.
    /// </summary>
    public static class PasswordHasher
    {
        /// <summary>
        /// Hash a plaintext password using SHA-256 and return the lowercase hex string.
        /// </summary>
        /// <param name="password">Plaintext password.</param>
        /// <returns>64-character lowercase hexadecimal SHA-256 hash.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="password"/> is null.</exception>
        public static string Hash(string password)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            byte[] bytes = Encoding.UTF8.GetBytes(password);
            byte[] hash = SHA256.HashData(bytes);
            StringBuilder sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// Decide whether a submitted value matches a stored hash.
        /// Accepts both plaintext (re-hashed) and an already-hashed value for convenience.
        /// </summary>
        /// <param name="submitted">Submitted value.</param>
        /// <param name="storedHash">Stored SHA-256 hash (hex).</param>
        /// <returns>True when the values match.</returns>
        public static bool Verify(string submitted, string storedHash)
        {
            if (String.IsNullOrEmpty(submitted) || String.IsNullOrEmpty(storedHash)) return false;
            if (String.Equals(submitted, storedHash, StringComparison.OrdinalIgnoreCase)) return true;
            return String.Equals(Hash(submitted), storedHash, StringComparison.OrdinalIgnoreCase);
        }
    }
}

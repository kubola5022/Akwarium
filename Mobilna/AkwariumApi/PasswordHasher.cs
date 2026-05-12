using System.Security.Cryptography;

namespace AkwariumApi
{
    public class PasswordHasher
    {
        public static string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Hasło nie może być puste.", nameof(password));

            const int iterations = 100_000;
            byte[] salt = new byte[16];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            byte[] key = pbkdf2.GetBytes(32);

            return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
        }
        public static bool VerifyPassword(string password, string stored)
        {
            if (string.IsNullOrEmpty(stored))
                return false;

            var parts = stored.Split('.');
            if (parts.Length != 3)
            {
                // stary format  plain text
                return password == stored;
            }

            if (!int.TryParse(parts[0], out var iterations))
                return false;

            byte[] salt;
            byte[] hash;

            try
            {
                salt = Convert.FromBase64String(parts[1]);
                hash = Convert.FromBase64String(parts[2]);
            }
            catch
            {
                return false;
            }

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            byte[] computed = pbkdf2.GetBytes(hash.Length);

            return CryptographicOperations.FixedTimeEquals(hash, computed);
        }
    }
}

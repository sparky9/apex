namespace ApexV2.Core.Authentication;

using System.Security.Cryptography;
using System.Text;

public interface IPasswordHasher
{
    (string hash, string salt, int iterations, string algorithm) HashPassword(string password, int? iterations = null);
    bool Verify(string password, string hash, string salt, int iterations, string algorithm, out bool needsRehash, int targetIterations = 120_000);
}

public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16; // 128-bit
    private const int KeySize = 32;  // 256-bit
    private const string Alg = "PBKDF2-SHA256";

    public (string hash, string salt, int iterations, string algorithm) HashPassword(string password, int? iterations = null)
    {
        if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password empty", nameof(password));
        var iters = iterations ?? 120_000;
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, iters, HashAlgorithmName.SHA256, KeySize);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes), iters, Alg);
    }

    public bool Verify(string password, string hash, string salt, int iterations, string algorithm, out bool needsRehash, int targetIterations = 120_000)
    {
        needsRehash = false;
        if (algorithm != Alg) return false; // unsupported
        var saltBytes = Convert.FromBase64String(salt);
        var expected = Convert.FromBase64String(hash);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, iterations, HashAlgorithmName.SHA256, expected.Length);
        var result = CryptographicOperations.FixedTimeEquals(actual, expected);
        if (result && iterations < targetIterations) needsRehash = true;
        return result;
    }
}

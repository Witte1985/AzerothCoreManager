using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace AzerothCoreManager.Infrastructure.Helpers;

/// <summary>
/// WoW SRP6 credential helper — shared between services that need to create game accounts.
/// </summary>
internal static class SrpHelper
{
    // SRP6 prime N used by WoW 3.3.5a
    private static readonly BigInteger N = BigInteger.Parse(
        "0894B645E89E1535BBDAD5B8B290650530801B18EBFBF5E8FAB3C82872A3E9BB7",
        System.Globalization.NumberStyles.HexNumber);

    private static readonly BigInteger G = new(7);

    internal static (string saltHex, string verifierHex) CalculateCredentials(string username, string password)
    {
        var identity = $"{username.ToUpperInvariant()}:{password.ToUpperInvariant()}";

        var salt = new byte[32];
        RandomNumberGenerator.Fill(salt);

        using var sha1 = SHA1.Create();
        var identityHash = sha1.ComputeHash(Encoding.UTF8.GetBytes(identity));

        var saltAndHash = new byte[salt.Length + identityHash.Length];
        Buffer.BlockCopy(salt, 0, saltAndHash, 0, salt.Length);
        Buffer.BlockCopy(identityHash, 0, saltAndHash, salt.Length, identityHash.Length);

        var x = new BigInteger(sha1.ComputeHash(saltAndHash), isUnsigned: true, isBigEndian: false);
        var verifier = BigInteger.ModPow(G, x, N);

        var verifierBytes = verifier.ToByteArray(isUnsigned: true, isBigEndian: false);
        var verifierResult = new byte[32];
        Buffer.BlockCopy(verifierBytes, 0, verifierResult, 0, Math.Min(verifierBytes.Length, 32));

        return (Convert.ToHexString(salt), Convert.ToHexString(verifierResult));
    }
}

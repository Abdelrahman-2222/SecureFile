using System.Security.Cryptography;

namespace SecureFile.Core.Cryptography;

/// <summary>
/// The two secret keys for one encrypted file: the AES-256 encryption key and the HMAC-SHA256 authentication key.
/// Dispose it as soon as the file operation ends; disposing overwrites both keys with zeros.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a class with <see cref="IDisposable"/>:</b> keys are secrets, so their lifetime must be short and visible
/// in the code. With <c>using FileKeys keys = …;</c> the keys are wiped when the block ends, even if an exception is
/// thrown, without relying on the garbage collector (which frees memory at an unknown time and does not clear it).
/// </para>
/// <para>
/// <b>Why the keys are exposed as <see cref="ReadOnlySpan{T}"/>:</b> callers can pass them straight to
/// <c>Aes.SetKey</c> and <c>IncrementalHash.CreateHMAC</c> but cannot keep or change them. After
/// <see cref="Dispose"/>, reading a key throws, so wiped keys are never used by mistake.
/// </para>
/// <para>Complexity: O(1) time and space (two 32-byte arrays).</para>
/// </remarks>
internal sealed class FileKeys : IDisposable
{
    private readonly byte[] _encryptionKey;
    private readonly byte[] _authenticationKey;
    private bool _disposed;

    /// <summary>
    /// Takes ownership of two key arrays: this object wipes them on <see cref="Dispose"/>, so the caller must not
    /// use them afterwards.
    /// </summary>
    /// <param name="encryptionKey">The 32-byte AES-256 key.</param>
    /// <param name="authenticationKey">The 32-byte HMAC-SHA256 key.</param>
    /// <exception cref="ArgumentNullException">A key is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A key is not exactly 32 bytes long.</exception>
    public FileKeys(byte[] encryptionKey, byte[] authenticationKey)
    {
        RequireKeyLength(encryptionKey, nameof(encryptionKey));
        RequireKeyLength(authenticationKey, nameof(authenticationKey));

        _encryptionKey = encryptionKey;
        _authenticationKey = authenticationKey;
    }

    /// <summary>Gets the 32-byte AES-256 key.</summary>
    /// <exception cref="ObjectDisposedException">The keys were already wiped.</exception>
    public ReadOnlySpan<byte> EncryptionKey
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _encryptionKey;
        }
    }

    /// <summary>Gets the 32-byte HMAC-SHA256 key.</summary>
    /// <exception cref="ObjectDisposedException">The keys were already wiped.</exception>
    public ReadOnlySpan<byte> AuthenticationKey
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _authenticationKey;
        }
    }

    /// <summary>Overwrites both keys with zeros. Calling it again does nothing harmful.</summary>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_encryptionKey);
        CryptographicOperations.ZeroMemory(_authenticationKey);
        _disposed = true;
    }

    /// <summary>Throws when a key is missing or does not have the AES-256 / HMAC-SHA256 key length.</summary>
    /// <param name="key">The key to check.</param>
    /// <param name="parameterName">The caller's parameter name, reported in the exception.</param>
    private static void RequireKeyLength(byte[] key, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(key, parameterName);
        if (key.Length != KeyDerivation.KeyLength)
        {
            throw new ArgumentException($"A key must be exactly {KeyDerivation.KeyLength} bytes long.", parameterName);
        }
    }
}

namespace Oms.Temporal.Codec;

using Temporalio.Sdk.Interop;
using Temporalio.Api.Common.V1;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

/// <summary>
/// Payload codec for AES-256-GCM encryption
/// Encrypts all workflow event history data at rest
/// </summary>
public class AesGcmPayloadCodec : IPayloadCodec
{
    private readonly byte[] _encryptionKey;
    private readonly ILogger<AesGcmPayloadCodec> _logger;
    private const string EncodingType = "AES-256-GCM";

    public AesGcmPayloadCodec(string encryptionKey, ILogger<AesGcmPayloadCodec> logger)
    {
        if (string.IsNullOrWhiteSpace(encryptionKey))
            throw new ArgumentException("Encryption key is required");

        // Key must be 32 bytes for AES-256
        _encryptionKey = Encoding.UTF8.GetBytes(encryptionKey.PadRight(32).Substring(0, 32));
        _logger = logger;
    }

    /// <summary>Encode (encrypt) payloads before storing in history</summary>
    public async IAsyncEnumerable<Payload> EncodeAsync(IAsyncEnumerable<Payload> payloads)
    {
        await foreach (var payload in payloads)
        {
            if (payload.Data.IsEmpty)
            {
                yield return payload;
                continue;
            }

            try
            {
                var encrypted = EncryptPayload(payload.Data.ToByteArray());
                var newPayload = new Payload();
                newPayload.Metadata[EncodingType] = new BytesValue
                {
                    Value = Google.Protobuf.ByteString.CopyFromUtf8(EncodingType)
                };
                newPayload.Data = Google.Protobuf.ByteString.CopyFrom(encrypted);

                yield return newPayload;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to encrypt payload");
                throw;
            }
        }
    }

    /// <summary>Decode (decrypt) payloads when retrieving from history</summary>
    public async IAsyncEnumerable<Payload> DecodeAsync(IAsyncEnumerable<Payload> payloads)
    {
        await foreach (var payload in payloads)
        {
            if (!payload.Metadata.ContainsKey(EncodingType))
            {
                yield return payload;
                continue;
            }

            try
            {
                var decrypted = DecryptPayload(payload.Data.ToByteArray());
                var newPayload = new Payload();
                newPayload.Data = Google.Protobuf.ByteString.CopyFrom(decrypted);

                yield return newPayload;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt payload");
                throw;
            }
        }
    }

    /// <summary>Encrypt data with AES-256-GCM</summary>
    private byte[] EncryptPayload(byte[] data)
    {
        using (var aes = new AesGcm(_encryptionKey))
        {
            byte[] nonce = new byte[12]; // 96 bits
            byte[] tag = new byte[16];   // 128 bits

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonce);
            }

            byte[] ciphertext = new byte[data.Length];
            aes.Encrypt(nonce, data, null, ciphertext, tag);

            // Result: nonce (12) + tag (16) + ciphertext (variable)
            var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);

            return result;
        }
    }

    /// <summary>Decrypt data with AES-256-GCM</summary>
    private byte[] DecryptPayload(byte[] encrypted)
    {
        using (var aes = new AesGcm(_encryptionKey))
        {
            const int nonceLength = 12;
            const int tagLength = 16;

            byte[] nonce = new byte[nonceLength];
            byte[] tag = new byte[tagLength];
            byte[] ciphertext = new byte[encrypted.Length - nonceLength - tagLength];

            Buffer.BlockCopy(encrypted, 0, nonce, 0, nonceLength);
            Buffer.BlockCopy(encrypted, nonceLength, tag, 0, tagLength);
            Buffer.BlockCopy(encrypted, nonceLength + tagLength, ciphertext, 0, ciphertext.Length);

            byte[] plaintext = new byte[ciphertext.Length];
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            return plaintext;
        }
    }
}

/// <summary>
/// Codec factory and configuration
/// Usage in Program.cs:
/// 
/// builder.Services.AddScoped(provider =>
/// {
///     var logger = provider.GetRequiredService&lt;ILogger&lt;AesGcmPayloadCodec&gt;&gt;();
///     var key = builder.Configuration["Temporal:EncryptionKey"];
///     return new AesGcmPayloadCodec(key, logger);
/// });
/// </summary>

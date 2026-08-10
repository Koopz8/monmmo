using System.Buffers.Binary;
using System.Text.Json;

namespace PokeMmo.Core.Net;

/// <summary>
/// Reads and writes messages over a stream.
/// <para>
/// TCP is a byte stream, not a message stream: a write of 200 bytes can arrive as
/// three reads, and two writes can arrive as one. Every frame is therefore prefixed
/// with its length, and reads loop until the whole frame is in hand. Assuming one
/// read yields one message is the classic way this breaks — and it breaks only under
/// load, which is the worst time to find out.
/// </para>
/// </summary>
public sealed class MessageChannel(Stream stream)
{
    /// <summary>
    /// Largest frame accepted. A malicious or corrupt length prefix must not be able
    /// to make the receiver allocate arbitrarily.
    /// </summary>
    public const int MaxFrameBytes = 1 << 20;

    private static readonly JsonSerializerOptions Json = new()
    {
        // Compact on the wire; the type discriminator is enough to read it back.
        WriteIndented = false,
    };

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>Serialises and sends one message, prefixed with its length.</summary>
    public async Task SendAsync(NetMessage message, CancellationToken cancellationToken = default)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message, Json);

        if (payload.Length > MaxFrameBytes)
            throw new InvalidOperationException($"Message is {payload.Length} bytes, over the {MaxFrameBytes} limit.");

        var frame = new byte[4 + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
        payload.CopyTo(frame, 4);

        // One writer at a time, or two concurrent sends can interleave their bytes and
        // corrupt both frames.
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Reads the next message, or returns null when the peer closed the connection
    /// cleanly between frames.
    /// </summary>
    public async Task<NetMessage?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        var header = new byte[4];
        if (!await ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false)) return null;

        int length = BinaryPrimitives.ReadInt32LittleEndian(header);

        if (length < 0 || length > MaxFrameBytes)
            throw new InvalidDataException($"Frame claims to be {length} bytes.");

        var payload = new byte[length];

        if (!await ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false))
            throw new InvalidDataException("Connection ended part-way through a frame.");

        return JsonSerializer.Deserialize<NetMessage>(payload, Json)
            ?? throw new InvalidDataException("Frame did not contain a message.");
    }

    /// <summary>
    /// Fills <paramref name="buffer"/> completely, looping over however many reads
    /// that takes. Returns false only if the stream ends before any of it arrives.
    /// </summary>
    private async Task<bool> ReadExactlyAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        int filled = 0;

        while (filled < buffer.Length)
        {
            int read = await stream
                .ReadAsync(buffer.AsMemory(filled), cancellationToken)
                .ConfigureAwait(false);

            if (read == 0) return filled != 0 ? throw new InvalidDataException("Stream ended mid-frame.") : false;

            filled += read;
        }

        return true;
    }
}

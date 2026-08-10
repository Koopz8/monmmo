using System.Security.Cryptography;
using System.Text;

namespace PokeMmo.RomExtract;

/// <summary>
/// A loaded Game Boy Advance cartridge image, plus safe accessors for reading
/// structures out of it.
/// <para>
/// Nothing in this assembly is ever linked by the server. The player's cartridge
/// is read locally, on their machine, and the bytes never leave it.
/// </para>
/// </summary>
public sealed class Rom
{
    /// <summary>GBA cartridge ROM is mapped at this address on the real hardware bus.</summary>
    public const uint BaseAddress = 0x0800_0000;

    private readonly byte[] _data;

    public Rom(byte[] data, string? sourcePath = null)
    {
        if (data.Length < 0xC0)
            throw new ArgumentException("File is too small to be a GBA ROM.", nameof(data));

        _data = data;
        SourcePath = sourcePath;

        Title = Encoding.ASCII.GetString(data, 0xA0, 12).TrimEnd('\0', ' ');
        GameCode = Encoding.ASCII.GetString(data, 0xAC, 4);
        MakerCode = Encoding.ASCII.GetString(data, 0xB0, 2);
        Version = data[0xBC];
    }

    public static Rom Load(string path) => new(File.ReadAllBytes(path), path);

    public string? SourcePath { get; }

    /// <summary>Internal cartridge title, e.g. <c>POKEMON FIRE</c>.</summary>
    public string Title { get; }

    /// <summary>Four-character game code, e.g. <c>BPRE</c> for FireRed (US).</summary>
    public string GameCode { get; }

    /// <summary>Two-character maker code; <c>01</c> is Nintendo.</summary>
    public string MakerCode { get; }

    /// <summary>Cartridge revision byte. 0 for the original release, 1 for rev1.</summary>
    public byte Version { get; }

    public int Length => _data.Length;

    public ReadOnlySpan<byte> Span => _data;

    private string? _sha1;

    /// <summary>Lowercase hex SHA-1 of the whole image, computed on first access.</summary>
    public string Sha1 => _sha1 ??= Convert.ToHexString(SHA1.HashData(_data)).ToLowerInvariant();

    /// <summary>True when <paramref name="address"/> is a pointer into this cartridge's address space.</summary>
    public bool IsRomAddress(uint address) =>
        address >= BaseAddress && address - BaseAddress < (uint)_data.Length;

    /// <summary>Converts a cartridge address to a file offset, or throws if it does not land inside the image.</summary>
    public int ToOffset(uint address)
    {
        if (!IsRomAddress(address))
            throw new ArgumentOutOfRangeException(
                nameof(address),
                $"0x{address:X8} is outside this ROM (size 0x{_data.Length:X}).");

        return (int)(address - BaseAddress);
    }

    public byte ReadU8(int offset) => _data[offset];

    public ushort ReadU16(int offset) => (ushort)(_data[offset] | (_data[offset + 1] << 8));

    public uint ReadU32(int offset) =>
        (uint)(_data[offset] | (_data[offset + 1] << 8) | (_data[offset + 2] << 16) | (_data[offset + 3] << 24));

    /// <summary>Reads a 32-bit cartridge pointer stored at <paramref name="offset"/>.</summary>
    public uint ReadPointer(int offset) => ReadU32(offset);

    public ReadOnlySpan<byte> Slice(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset + length > _data.Length)
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                $"Slice [0x{offset:X}, 0x{offset + length:X}) escapes the ROM (size 0x{_data.Length:X}).");

        return _data.AsSpan(offset, length);
    }

    /// <summary>
    /// Finds every occurrence of <paramref name="pattern"/>, optionally restricted to
    /// offsets that are a multiple of <paramref name="alignment"/>.
    /// </summary>
    public IEnumerable<int> FindAll(ReadOnlyMemory<byte> pattern, int alignment = 1)
    {
        var results = new List<int>();
        ReadOnlySpan<byte> pat = pattern.Span;
        if (pat.Length == 0) return results;

        for (int i = 0; i + pat.Length <= _data.Length; i += alignment)
        {
            if (_data.AsSpan(i, pat.Length).SequenceEqual(pat))
                results.Add(i);
        }

        return results;
    }
}

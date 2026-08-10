namespace PokeMmo.RomExtract;

public enum RomGame
{
    Unknown,
    FireRed,
    LeafGreen,
    Ruby,
    Sapphire,
    Emerald,
}

/// <summary>
/// Identifies which cartridge the player supplied.
/// <para>
/// Identification is driven primarily by the cartridge header (game code + revision)
/// rather than by a hash allowlist, because the header is self-describing and cannot
/// silently mismatch. The SHA-1 is still computed and reported: known-good hashes are
/// published by the pret decompilation projects, and matching one means the extractor
/// is looking at exactly the image those projects document.
/// </para>
/// </summary>
public sealed record RomIdentity(
    RomGame Game,
    string GameCode,
    byte Version,
    string Sha1,
    bool Sha1IsKnown,
    string Description)
{
    /// <summary>
    /// Published SHA-1 hashes for the images the pret decompilations target.
    /// Source: pret/pokefirered <c>firered.sha1</c> and <c>firered_rev1.sha1</c>.
    /// </summary>
    private static readonly Dictionary<string, string> KnownHashes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc"] = "Pokémon FireRed (US) rev 0",
        ["dd5945db9b930750cb39d00c84da8571feebf417"] = "Pokémon FireRed (US) rev 1",
    };

    public bool IsFireRed => Game == RomGame.FireRed;

    public static RomIdentity Identify(Rom rom)
    {
        RomGame game = rom.GameCode switch
        {
            "BPRE" => RomGame.FireRed,
            "BPGE" => RomGame.LeafGreen,
            "AXVE" => RomGame.Ruby,
            "AXPE" => RomGame.Sapphire,
            "BPEE" => RomGame.Emerald,
            _ => RomGame.Unknown,
        };

        bool known = KnownHashes.TryGetValue(rom.Sha1, out string? matched);

        string description = known
            ? matched!
            : game == RomGame.Unknown
                ? $"Unrecognised cartridge (code '{rom.GameCode}', title '{rom.Title}')"
                : $"{game} (code '{rom.GameCode}', rev {rom.Version}) — hash not in the known-good list";

        return new RomIdentity(game, rom.GameCode, rom.Version, rom.Sha1, known, description);
    }
}

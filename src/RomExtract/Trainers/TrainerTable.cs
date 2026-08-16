using PokeMmo.Core.Data;

namespace PokeMmo.RomExtract.Trainers;

/// <summary>
/// One trainer, as the cartridge records them.
/// <para>
/// The record is 40 bytes: some flags, a class and a picture, a fixed-width name, four
/// item slots, a double-battle flag, the AI bits, and finally a party size and a
/// pointer to the party itself.
/// </para>
/// <para>
/// The name is read here and goes no further than this process. It is cartridge text,
/// and the rules file the server loads carries no text at all — the client turns the
/// id back into a name from the image on the player's own machine, exactly as it does
/// for species and moves.
/// </para>
/// </summary>
public sealed record TrainerRecord(
    int Id,
    int Offset,
    int Class,
    int PicId,
    string Name,
    bool IsDouble,
    IReadOnlyList<TrainerMon> Party)
{
    /// <summary>
    /// Byte two of the record, which has been read past since trainers were first read.
    /// <para>
    /// The class is byte one and the picture is byte three, and the byte between them went
    /// nowhere — the same shape as <c>MapHeaderRecord.Music</c>, which carried the map's song
    /// number for a hundred and sixty milestones before anything asked for it.
    /// </para>
    /// <para>
    /// It is <b>read</b> and it is deliberately not named. What can be said from the file is
    /// that its low seven bits take one of a small handful of values across the whole trainer
    /// table and its top bit does not — which is the shape of a small index and a flag packed
    /// into one byte, and is why the two are separated here rather than reported as one
    /// number. What the index selects is not in any table on the cartridge, so calling it a
    /// music id here would be importing a fact from somewhere else and printing it as though
    /// it had been found. The distribution is printed instead; see the trainer section of the
    /// dump.
    /// </para>
    /// </summary>
    public int PackedByte { get; init; }

    /// <summary>The low seven bits of <see cref="PackedByte"/> — a small index.</summary>
    public int PackedIndex => PackedByte & 0x7F;

    /// <summary>The top bit of <see cref="PackedByte"/>, which varies independently of it.</summary>
    public bool PackedFlag => (PackedByte & 0x80) != 0;

    public const int RecordSizeBytes = 40;

    /// <summary>The most a party can hold. A count past this is not a trainer record.</summary>
    public const int MaxPartySize = 6;

    private const int NameOffset = 4;
    private const int NameLength = 12;
    private const int DoubleOffset = 24;

    /// <summary>Between the class and the picture, and read past until now.</summary>
    private const int PackedByteOffset = 2;
    private const int PartySizeOffset = 32;
    private const int PartyPointerOffset = 36;

    /// <summary>Party members carry their own four moves rather than a level-up set.</summary>
    private const int CustomMoves = 1;

    /// <summary>Party members carry a held item.</summary>
    private const int HeldItem = 2;

    /// <summary>
    /// Reads a record, or returns null when the bytes are not one.
    /// <para>
    /// The discriminating checks are the party: a size between one and six, a pointer
    /// that lands inside the image, and a first member whose species and level are both
    /// in range. Flags and a name on their own are far too easy to satisfy by accident;
    /// a pointer that has to lead somewhere with a plausible creature at the end of it
    /// is not.
    /// </para>
    /// </summary>
    public static TrainerRecord? TryParse(Rom rom, int offset, int id, int speciesCount)
    {
        if (offset < 0 || offset + RecordSizeBytes > rom.Length) return null;

        byte flags = rom.ReadU8(offset);
        if (flags > (CustomMoves | HeldItem)) return null;

        if (rom.ReadU8(offset + DoubleOffset) > 1) return null;

        // Three bytes of padding after the double-battle flag. Compilers zero-fill
        // these, and requiring it costs nothing and rules out a great deal of noise.
        if (rom.ReadU8(offset + DoubleOffset + 1) != 0) return null;
        if (rom.ReadU8(offset + DoubleOffset + 2) != 0) return null;
        if (rom.ReadU8(offset + DoubleOffset + 3) != 0) return null;

        uint size = rom.ReadU32(offset + PartySizeOffset);
        if (size is 0 or > MaxPartySize) return null;

        if (rom.ToOffsetOrNull(rom.ReadU32(offset + PartyPointerOffset)) is not { } party) return null;

        int stride = MemberSizeBytes(flags);
        if (party + (int)size * stride > rom.Length) return null;

        var members = new List<TrainerMon>((int)size);

        for (int i = 0; i < size; i++)
        {
            if (ReadMember(rom, party + i * stride, flags, speciesCount) is not { } member) return null;
            members.Add(member);
        }

        return new TrainerRecord(
            id,
            offset,
            rom.ReadU8(offset + 1),
            rom.ReadU8(offset + 3),
            GameText.Decode(rom.Slice(offset + NameOffset, NameLength)),
            rom.ReadU8(offset + DoubleOffset) == 1,
            members)
        {
            PackedByte = rom.ReadU8(offset + PackedByteOffset),
        };
    }

    /// <summary>
    /// How wide one party member is.
    /// <para>
    /// Four shapes, chosen by two flag bits, and they collapse to two widths. Without
    /// custom moves a member is eight bytes — the last two being either a held item or
    /// nothing at all — and with them it is sixteen. Getting this wrong reads the second
    /// member from the middle of the first, which produces a party of plausible
    /// nonsense rather than an error.
    /// </para>
    /// </summary>
    public static int MemberSizeBytes(int flags) => (flags & CustomMoves) != 0 ? 16 : 8;

    private static TrainerMon? ReadMember(Rom rom, int at, int flags, int speciesCount)
    {
        int level = rom.ReadU16(at + 2);
        int species = rom.ReadU16(at + 4);

        if (level is < 1 or > 100) return null;
        if (species < 1 || species > speciesCount) return null;

        bool item = (flags & HeldItem) != 0;
        bool moves = (flags & CustomMoves) != 0;

        int held = item ? rom.ReadU16(at + 6) : 0;
        int movesAt = at + (item ? 8 : 6);

        var known = new List<int>();

        if (moves)
        {
            for (int i = 0; i < 4; i++)
            {
                int move = rom.ReadU16(movesAt + i * 2);
                if (move != 0) known.Add(move);
            }
        }

        return new TrainerMon(species, level, held, known);
    }

    /// <summary>
    /// Why the bytes at an offset are not a record.
    /// <para>
    /// The same job <c>Explain</c> does for the sprite table, and for the same reason:
    /// a table found a few entries late is not a failure anybody notices, it just makes
    /// every trainer id somebody else's.
    /// </para>
    /// </summary>
    public static string Explain(Rom rom, int offset, int speciesCount)
    {
        if (offset < 0 || offset + RecordSizeBytes > rom.Length) return "outside the image";

        byte flags = rom.ReadU8(offset);
        if (flags > (CustomMoves | HeldItem)) return $"party flags 0x{flags:X2} has bits nothing uses";

        if (rom.ReadU8(offset + DoubleOffset) > 1) return "the double-battle flag is not a flag";

        for (int i = 1; i <= 3; i++)
        {
            if (rom.ReadU8(offset + DoubleOffset + i) != 0) return $"padding byte {i} is not zero";
        }

        uint size = rom.ReadU32(offset + PartySizeOffset);
        if (size == 0) return "an empty party";
        if (size > MaxPartySize) return $"a party of {size}";

        if (rom.ToOffsetOrNull(rom.ReadU32(offset + PartyPointerOffset)) is not { } party)
            return "the party pointer is not a pointer";

        int stride = MemberSizeBytes(flags);
        if (party + (int)size * stride > rom.Length) return "the party runs past the end of the image";

        for (int i = 0; i < size; i++)
        {
            int at = party + i * stride;
            int level = rom.ReadU16(at + 2);
            int species = rom.ReadU16(at + 4);

            if (level is < 1 or > 100) return $"member {i} is level {level}";
            if (species < 1 || species > speciesCount) return $"member {i} is species {species}";
        }

        return "reads as a record";
    }
}

/// <summary>
/// One member of a trainer's party, as read off the cartridge.
/// <para>
/// An empty <see cref="Moves"/> means "whatever this species knows at this level",
/// which is what most trainers in the games actually specify. Filling it in here would
/// need the learnsets, and the party is read long before anybody has decided which
/// rules file it is going into.
/// </para>
/// </summary>
public sealed record TrainerMon(int Species, int Level, int HeldItem, IReadOnlyList<int> Moves)
{
    public TrainerMember ToMember() => new(Species, Level, HeldItem, [.. Moves]);

    /// <summary>Compares move lists by their contents, for the same reason everything
    /// else in this project that holds a list does.</summary>
    public bool Equals(TrainerMon? other) =>
        other is not null &&
        Species == other.Species &&
        Level == other.Level &&
        HeldItem == other.HeldItem &&
        Moves.SequenceEqual(other.Moves);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add(Species);
        hash.Add(Level);
        hash.Add(HeldItem);

        foreach (int move in Moves) hash.Add(move);

        return hash.ToHashCode();
    }
}

/// <summary>
/// Finds the trainer table and reads it.
/// <para>
/// Located by structure like everything else. The signature is a long run of 40-byte
/// records whose party pointers all lead to plausible creatures — which is a strong
/// one, because it requires arithmetic in one place to agree with data somewhere else
/// entirely, several hundred times in a row.
/// </para>
/// </summary>
public static class TrainerTable
{
    /// <summary>
    /// Records a run must contain before it is believed to be the table.
    /// <para>
    /// Real cartridges hold several hundred. This is set well below that so that a
    /// fixture does not have to be enormous, and well above anything that turns up by
    /// chance.
    /// </para>
    /// </summary>
    private const int MinimumRun = 32;

    /// <summary>
    /// Dead entries tolerated in a row before a run is considered finished.
    /// <para>
    /// The first trainer is a placeholder with no party at all, and there are gaps
    /// further in where entries were removed during development. A run that ends at the
    /// first hole is not the table — it is the part of the table before its first hole.
    /// </para>
    /// </summary>
    private const int MaxDeadInARow = 8;

    /// <summary>
    /// True when a slot is a trainer-shaped hole: no party, no pointer, nothing to
    /// fight. The first entry of a real table is one of these — a placeholder standing
    /// in for "no trainer" so that no real trainer has id zero.
    /// </summary>
    private static bool IsBlankRecord(Rom rom, int offset)
    {
        if (offset < 0 || offset + TrainerRecord.RecordSizeBytes > rom.Length) return false;

        return rom.ReadU8(offset) <= 3 &&
               rom.ReadU32(offset + 32) == 0 &&
               rom.ReadU32(offset + 36) == 0;
    }

    /// <summary>
    /// Finds the table. Returns the offset of the <em>first</em> record, which is
    /// trainer zero, so that an index into the table is a trainer id.
    /// </summary>
    public static int? Locate(Rom rom, int speciesCount, Action<string>? log = null)
    {
        int stride = TrainerRecord.RecordSizeBytes;

        for (int offset = 0; offset + MinimumRun * stride <= rom.Length; offset += 4)
        {
            // A table cannot begin with a hole. Starting one record late is not a
            // failure anybody would see — it just shifts every trainer id by one.
            if (TrainerRecord.TryParse(rom, offset, 0, speciesCount) is null) continue;

            int valid = 0;
            int length = 0;
            int dead = 0;

            while (offset + (length + 1) * stride <= rom.Length)
            {
                if (TrainerRecord.TryParse(rom, offset + length * stride, 0, speciesCount) is not null)
                {
                    valid++;
                    dead = 0;
                }
                else if (++dead > MaxDeadInARow)
                {
                    break;
                }

                length++;
            }

            if (valid < MinimumRun)
            {
                if (length > 1) offset += length * stride - 4;
                continue;
            }

            int start = BackUpOverThePlaceholder(rom, offset, log);

            log?.Invoke(
                $"  trainers: {valid} records across {length} entries " +
                $"at 0x{Rom.BaseAddress + (uint)start:X8}");

            return start;
        }

        return null;
    }

    /// <summary>
    /// Steps back onto the placeholder at the front of the table, when there is one.
    /// <para>
    /// The first entry of a real table has no party, so it does not read as a record and
    /// the run starts at trainer one. Calling that trainer zero shifts every id by one —
    /// which is not a failure anybody sees, it just gives every trainer somebody else's
    /// creatures. The sprite table did exactly this and nothing failed.
    /// </para>
    /// <para>
    /// Only a run of <em>exactly one</em> blank counts. Zero padding before an unrelated
    /// block reads as blank too, and there is always plenty of it — but there is a lot
    /// more than one slot's worth, which is what tells the two apart.
    /// </para>
    /// </summary>
    private static int BackUpOverThePlaceholder(Rom rom, int offset, Action<string>? log)
    {
        int stride = TrainerRecord.RecordSizeBytes;
        int blanks = 0;

        while (blanks < 4 && IsBlankRecord(rom, offset - (blanks + 1) * stride)) blanks++;

        if (blanks != 1) return offset;

        log?.Invoke("  trainers: table starts one slot earlier, on the empty placeholder");
        return offset - stride;
    }

    /// <summary>
    /// Reads the table from its first record until it stops being one.
    /// <para>
    /// Holes are skipped rather than ending the read, and nothing is renumbered: the
    /// index into the table is the trainer id, and a script asking for trainer 214 has
    /// to get the record that was 214th whether or not 3 and 4 were empty.
    /// </para>
    /// </summary>
    public static List<TrainerRecord> Read(Rom rom, int table, int speciesCount, int maxTrainers = 1024)
    {
        var trainers = new List<TrainerRecord>();

        int dead = 0;

        for (int id = 0; id < maxTrainers; id++)
        {
            int at = table + id * TrainerRecord.RecordSizeBytes;
            if (at + TrainerRecord.RecordSizeBytes > rom.Length) break;

            if (TrainerRecord.TryParse(rom, at, id, speciesCount) is { } record)
            {
                trainers.Add(record);
                dead = 0;
                continue;
            }

            if (++dead > MaxDeadInARow) break;
        }

        return trainers;
    }
}

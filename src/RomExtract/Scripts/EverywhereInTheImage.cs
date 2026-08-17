namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// One place in the whole image where a flag is turned on or off.
/// </summary>
/// <param name="Offset">Where the command byte sits in the file.</param>
/// <param name="Flag">The flag it moves.</param>
/// <param name="Sets">True for <c>setflag</c>, false for <c>clearflag</c>.</param>
/// <param name="ReadsAsAScript">
/// True when the bytes from here decode as commands and reach an end, a return or a goto.
/// <para>
/// The discriminator, and the reason a raw byte scan is usable at all. Three bytes recur in
/// sixteen megabytes by accident about once, and an accident lands in the middle of somebody
/// else's argument where the bytes after it are not commands.
/// </para>
/// </param>
/// <param name="Opened">
/// True when the map scan's own reading of the world decoded this very byte as a command.
/// <b>This is the measurement.</b> A site the map scan never opened is a site every "nothing
/// in the world sets this flag" in this project has been silent about.
/// </param>
public sealed record FlagSite(int Offset, int Flag, bool Sets, bool ReadsAsAScript, bool Opened)
{
    public uint Address => Rom.BaseAddress + (uint)Offset;

    public override string ToString() =>
        $"0x{Offset:X6} {(Sets ? "setflag" : "clearflag")} 0x{Flag:X4}";
}

/// <summary>
/// One place in the whole image where a number is put into one of the story's own variables.
/// </summary>
/// <param name="How">Which command — <c>setvar</c>, <c>addvar</c>, <c>subvar</c>, <c>copyvarifnotzero</c>.</param>
/// <param name="Value">
/// The second word. A number for everything but the copying one, where it names another
/// variable and what is in it is not knowable from here — said out loud rather than printed as
/// though it were a value.
/// </param>
public sealed record VariableSite(
    int Offset, int Variable, byte How, int Value, bool ReadsAsAScript, bool Opened)
{
    public uint Address => Rom.BaseAddress + (uint)Offset;

    /// <summary>True when this is the one command whose second word is not a number.</summary>
    public bool Copies => How == 0x1A;

    public override string ToString() =>
        Copies
            ? $"0x{Offset:X6} {ScriptCommands.NameOf(How)} 0x{Variable:X4} from 0x{Value:X4}"
            : $"0x{Offset:X6} {ScriptCommands.NameOf(How)} 0x{Variable:X4}, {Value}";
}

/// <summary>
/// One place in the image holding a pointer at, or just above, an address.
/// </summary>
/// <param name="Offset">Where the four bytes sit.</param>
/// <param name="Points">What they point at.</param>
/// <param name="Opcode">
/// The script command this pointer is the argument of, or zero when it is not one. Read from
/// the byte before it for <c>call</c> and <c>goto</c>, and from two bytes before it for the
/// conditional pair and <c>loadpointer</c>, because that is where each of them puts its
/// pointer.
/// </param>
public sealed record NamesIt(int Offset, uint Points, byte Opcode)
{
    /// <summary>True when a script jumps here — the only kind that is a way in.</summary>
    public bool AJump => Opcode is ScriptCommands.Call or ScriptCommands.Goto
        or ScriptCommands.GotoIf or ScriptCommands.CallIf;

    /// <summary>
    /// True when this is four aligned bytes that no command owns.
    /// <para>
    /// <b>A finding rather than a miss.</b> Script pointers in this cartridge sit at whatever
    /// offset the command before them left; a pointer on a four-byte boundary with no opcode in
    /// front of it is a table entry or a literal in the game's own code — which is to say the
    /// thing on the far side of the code boundary, with an address on it.
    /// </para>
    /// </summary>
    public bool ALiteral => Opcode == 0 && Offset % 4 == 0;

    public override string ToString() =>
        AJump ? $"0x{Offset:X6}  {ScriptCommands.NameOf(Opcode)} 0x{Points:X8}"
        : ALiteral ? $"0x{Offset:X6}  a literal holding 0x{Points:X8}"
        : $"0x{Offset:X6}  four loose bytes holding 0x{Points:X8}";
}

/// <summary>
/// Reading the file rather than the world.
/// <para>
/// <b>Every instrument in this project so far starts at a map.</b> It gathers the scripts the
/// maps point at, follows the calls and gotos out of them, and reports on what it found —
/// which is the right shape for almost every question and is silently the wrong shape for one:
/// <em>is there anything here the maps do not point at?</em> A scan that begins at the maps
/// cannot answer that, and it does not fail when asked. It comes back the same as a scan that
/// looked everywhere and found nothing.
/// </para>
/// <para>
/// Three times last session the answer was in a part of the file the scan does not open. So
/// this one does not start anywhere. It scans all sixteen megabytes for the three bytes that
/// turn a flag on, and then asks of every hit the only question that matters: <b>did the map
/// scan ever decode this byte?</b>
/// </para>
/// <para>
/// <b>It can come back empty, and it says how empty empty is.</b> Three bytes recur by chance
/// about once in an image this size, so a lone hit that does not decode as a script is
/// probably noise and <see cref="ByChance"/> prints the number rather than leaving the reader
/// to feel confident.
/// </para>
/// </summary>
public static class EverywhereInTheImage
{
    private const byte SetFlag = 0x29;
    private const byte ClearFlag = 0x2A;

    /// <summary>
    /// How many hits a pattern this long would be expected to have in this image by accident.
    /// <para>
    /// The error bar on a byte scan, and the difference between a finding and a coincidence.
    /// Printed rather than reasoned about, because "three bytes is surely specific enough" is
    /// the kind of sentence that is right until the image is sixteen megabytes.
    /// </para>
    /// </summary>
    public static double ByChance(Rom rom, int patternBytes) =>
        rom.Length / Math.Pow(256, patternBytes);

    /// <summary>Nothing opened this byte.</summary>
    public const int Nobody = -1;

    /// <summary>
    /// Which script opened each byte of the image, or <see cref="Nobody"/>.
    /// <para>
    /// <b>The blind spot, with a size on it.</b> Not how many scripts were opened — that number
    /// has been printed for a session and it cannot be compared with anything. This is which
    /// bytes, so that any address at all can be asked whether it was inside or outside, and
    /// "the scan never looked here" stops being a suspicion.
    /// </para>
    /// <para>
    /// <b>And <em>whose</em>, which is the half this came back without.</b> A climb that reaches
    /// an opened byte can say "a map leads here" and stop, which is true and is not an answer:
    /// the next question is always which map, and an index into the caller's own list of scripts
    /// answers it for the cost of three bytes a byte. The first script to decode a byte owns it;
    /// several may reach the same shared block, and which one is named is arbitrary among them
    /// rather than wrong.
    /// </para>
    /// </summary>
    public static int[] Opened(Rom rom, IReadOnlyList<SetsAFlag> scripts, int maxScripts = 96)
    {
        var covered = new int[rom.Length];

        Array.Fill(covered, Nobody);

        var seen = new HashSet<uint>();

        for (var which = 0; which < scripts.Count; which++)
        {
            foreach (uint block in ScriptReader.Reachable(rom, scripts[which].Address, maxScripts))
            {
                if (!seen.Add(block)) continue;

                foreach (ScriptCommand command in ScriptReader.Read(rom, block))
                {
                    for (int i = command.Offset; i < command.Offset + 1 + command.Arguments.Length; i++)
                    {
                        if (i >= 0 && i < covered.Length && covered[i] == Nobody) covered[i] = which;
                    }
                }
            }
        }

        return covered;
    }

    /// <summary>
    /// Everywhere in the file a flag is turned on or off, whether or not any map leads there.
    /// </summary>
    /// <param name="covered">
    /// What the map scan decoded, from <see cref="Opened"/>. Null when the caller has not
    /// worked it out, in which case every site reports as unopened — which is honest about the
    /// caller rather than about the file, and is why it is a parameter and not a default.
    /// </param>
    public static IReadOnlyList<FlagSite> Moves(Rom rom, int flag, int[]? covered = null)
    {
        var sites = new List<FlagSite>();

        byte low = (byte)(flag & 0xFF);
        byte high = (byte)(flag >> 8);

        foreach ((byte code, bool sets) in new[] { (SetFlag, true), (ClearFlag, false) })
        {
            foreach (int offset in rom.FindAll(new byte[] { code, low, high }))
            {
                sites.Add(new FlagSite(
                    offset,
                    flag,
                    sets,
                    ReadsAsAScript(rom, Rom.BaseAddress + (uint)offset),
                    covered is not null && offset < covered.Length && covered[offset] != Nobody));
            }
        }

        return [.. sites.OrderBy(s => s.Offset)];
    }

    /// <summary>
    /// Everywhere in the file a number is put into one of the story's own variables.
    /// <para>
    /// <b>The same question as <see cref="Moves"/>, for the other half of the story's memory.</b>
    /// A gate is a flag or it is a variable, and this project has been able to hunt one of those
    /// through the whole image and not the other since <c>--in-the-image</c> was written. The
    /// starter — the only creature in the game a player chooses — is behind
    /// <c>0x4055 == 2</c>, and the only way to say who puts a two in it has been to grep by eye.
    /// </para>
    /// <para>
    /// All four commands that write one, because a variable set once and added to afterwards is
    /// the commonest shape a counter has, and looking only for <c>setvar</c> would report the
    /// count that starts a story and miss every step of it.
    /// </para>
    /// </summary>
    public static IReadOnlyList<VariableSite> Writes(Rom rom, int variable, int[]? covered = null)
    {
        var sites = new List<VariableSite>();

        byte low = (byte)(variable & 0xFF);
        byte high = (byte)(variable >> 8);

        foreach (byte code in Writers)
        {
            foreach (int offset in rom.FindAll(new byte[] { code, low, high }))
            {
                if (offset + 5 > rom.Length) continue;

                sites.Add(new VariableSite(
                    offset,
                    variable,
                    code,
                    rom.ReadU16(offset + 3),
                    ReadsAsAScript(rom, Rom.BaseAddress + (uint)offset),
                    covered is not null && offset < covered.Length && covered[offset] != Nobody));
            }
        }

        return [.. sites.OrderBy(s => s.Offset)];
    }

    /// <summary>
    /// Every variable written anywhere in the file, with how many places write it.
    /// <para>
    /// <b>The readable difference between a story counter and a scratch pad.</b> Milestone 173
    /// established that <c>0x4001</c> is scratch by counting: 285 scripts write it, so a
    /// comparison on it is a switch a script computes and reads back rather than a precondition.
    /// The same count, taken across every variable at once, is the shape of the whole
    /// distinction — and whether there is a clean line between the two kinds is a fact about
    /// this cartridge that can be looked at rather than assumed.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<int, int> EveryVariableWritten(Rom rom)
    {
        var found = new Dictionary<int, int>();

        for (int offset = 0; offset + 5 <= rom.Length; offset++)
        {
            if (!Writers.Contains(rom.ReadU8(offset))) continue;
            if (!ReadsAsAScript(rom, Rom.BaseAddress + (uint)offset)) continue;

            int variable = rom.ReadU16(offset + 1);

            found[variable] = found.GetValueOrDefault(variable) + 1;
        }

        return found;
    }

    /// <summary>The four commands that put a number in a variable, in the order they were derived.</summary>
    private static readonly byte[] Writers = [0x16, 0x17, 0x18, 0x1A];

    /// <summary>
    /// Every flag moved anywhere in the file, by flag, in one pass.
    /// <para>
    /// <b>The whole code boundary, re-asked of the file instead of the world.</b> Two hundred
    /// and forty-eight flags gate somebody and are moved by no script any map leads to; that
    /// sentence has been the boundary for two sessions and it is a sentence about the scripts
    /// the maps reach. Asking it of every byte instead is one pass, and it turns "nothing
    /// moves this" into two very different findings: moved by script somewhere nothing leads
    /// to, or not moved by any script that exists.
    /// </para>
    /// <para>
    /// Only hits that read as script are kept, because a whole-file sweep is otherwise mostly
    /// noise: a hundred and thirty thousand raw hits in a sixteen-megabyte image, of which
    /// almost all land in the middle of somebody else's argument.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<int, IReadOnlyList<FlagSite>> EveryFlagMoved(
        Rom rom, int[]? covered = null)
    {
        var found = new Dictionary<int, List<FlagSite>>();

        for (int offset = 0; offset + 3 <= rom.Length; offset++)
        {
            byte code = rom.ReadU8(offset);

            if (code is not (SetFlag or ClearFlag)) continue;
            if (!ReadsAsAScript(rom, Rom.BaseAddress + (uint)offset)) continue;

            int flag = rom.ReadU16(offset + 1);

            if (!found.TryGetValue(flag, out List<FlagSite>? sites)) found[flag] = sites = [];

            sites.Add(new FlagSite(
                offset,
                flag,
                code == SetFlag,
                true,
                covered is not null && offset < covered.Length && covered[offset] != Nobody));
        }

        return found.ToDictionary(p => p.Key, p => (IReadOnlyList<FlagSite>)p.Value);
    }

    /// <summary>
    /// One gating flag nothing in the world moves, and what the rest of the file says about it.
    /// </summary>
    /// <param name="Unopened">Sites moving it that the map scan never decoded.</param>
    /// <param name="JumpedInto">
    /// The ones a script jumps to on purpose. <b>The promotion from candidate to job.</b>
    /// "Reads as script" is a weak filter — the reversal control says how weak — and a site
    /// something jumps into is not a coincidence twice over.
    /// </param>
    public sealed record OutsideTheWorld(
        int Flag, IReadOnlyList<FlagSite> Unopened, IReadOnlyList<FlagSite> JumpedInto);

    /// <summary>
    /// Which flags on the code boundary the file has something to say about after all.
    /// <para>
    /// <b>Kept here rather than in whoever is printing.</b> The rule that decides which flags
    /// are news — a site nothing opened, and a jump into it — is exactly the kind of rule this
    /// project has three times written in the reporting layer, which has no tests, and three
    /// times got wrong somewhere no fixture could reach.
    /// </para>
    /// <para>
    /// A flag whose every site the map scan already opened is not on this list. It is a flag
    /// <c>--flags</c> has been describing correctly all along, and putting it here would bury
    /// the new ones under two hundred old ones.
    /// </para>
    /// </summary>
    public static IReadOnlyList<OutsideTheWorld> PastTheBoundary(
        Rom rom,
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index,
        IEnumerable<int> boundary,
        IReadOnlyDictionary<int, IReadOnlyList<FlagSite>> moved,
        int slack = 192)
    {
        var found = new List<OutsideTheWorld>();

        foreach (int flag in boundary)
        {
            if (!moved.TryGetValue(flag, out IReadOnlyList<FlagSite>? sites)) continue;

            List<FlagSite> unopened = [.. sites.Where(s => !s.Opened)];

            if (unopened.Count == 0) continue;

            found.Add(new OutsideTheWorld(
                flag,
                unopened,
                [.. unopened.Where(s => WhoNames(rom, index, s.Address, slack).Any(n => n.AJump))]));
        }

        return [.. found.OrderByDescending(f => f.JumpedInto.Count).ThenBy(f => f.Flag)];
    }

    /// <summary>
    /// What the sweep finds in this same file with the bytes reversed — the noise floor.
    /// <para>
    /// <b>The control, and this instrument does not mean anything without it.</b> "Reads as
    /// script" sounds like a strong filter and is not: on sixteen megabytes of random bytes the
    /// sweep still comes back with thousands of sites, because a <c>setflag</c> followed by
    /// something that happens to decode and end is three or four bytes of luck.
    /// </para>
    /// <para>
    /// Reversing the image keeps every byte and every byte's frequency exactly as it is and
    /// destroys every command boundary in it. So whatever the sweep finds in the reversal is
    /// what it would find in a file with these statistics and no scripts at all — which is the
    /// only honest thing to put next to the real count.
    /// </para>
    /// </summary>
    /// <param name="slack">The same reach the real climb uses, or the control is not one.</param>
    /// <returns>
    /// How many sites the sweep finds there, and how many of those something jumps into.
    /// <b>Both, because both are printed.</b> A control on the raw count and none on the
    /// filtered one leaves the filtered one looking rigorous by association.
    /// </returns>
    public static (int Sites, int JumpedInto) NoiseFloor(Rom rom, int slack = 192)
    {
        byte[] backwards = rom.Span.ToArray();

        Array.Reverse(backwards);

        var nowhere = new Rom(backwards);

        IReadOnlyList<FlagSite> found = [.. EveryFlagMoved(nowhere).Values.SelectMany(sites => sites)];

        IReadOnlyDictionary<uint, IReadOnlyList<int>> index = PointerIndex(nowhere);

        return (
            found.Count,
            found.Count(s => WhoNames(nowhere, index, s.Address, slack).Any(n => n.AJump)));
    }

    /// <summary>
    /// Where two flags are moved close enough together to be one piece of script.
    /// <para>
    /// <b>The question this was built for.</b> One flag holds eight people in place on SAFFRON
    /// and another keeps seven off the same map; one scene does both halves and only one half
    /// has ever been visible, because being invisible looks exactly like nothing at all.
    /// Two lists of sites do not say that. Sites within a few dozen bytes of each other do.
    /// </para>
    /// </summary>
    public static IReadOnlyList<(FlagSite First, FlagSite Second)> Together(
        IEnumerable<FlagSite> left, IEnumerable<FlagSite> right, int within = 128)
    {
        List<FlagSite> theirs = [.. right];

        return
        [
            .. from a in left
               from b in theirs
               where a.Offset != b.Offset && Math.Abs(a.Offset - b.Offset) <= within
               orderby Math.Abs(a.Offset - b.Offset)
               select (a, b),
        ];
    }

    /// <summary>
    /// Every four bytes in the file holding a pointer to an address, indexed once.
    /// <para>
    /// Built whole rather than searched per question, because a climb asks it a few dozen
    /// times and each pass is sixteen million reads. Only values that land inside this image
    /// are kept, which on a sixteen-megabyte cartridge is one byte in two hundred and
    /// fifty-six by accident — so the index is mostly noise by count and the classification on
    /// each hit is what separates them.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<uint, IReadOnlyList<int>> PointerIndex(Rom rom)
    {
        var index = new Dictionary<uint, List<int>>();

        for (int offset = 0; offset + 4 <= rom.Length; offset++)
        {
            // The top byte first: it rules out two hundred and fifty-five in every two
            // hundred and fifty-six without a read, and this loop runs sixteen million times.
            if (rom.ReadU8(offset + 3) != 0x08) continue;

            uint value = rom.ReadU32(offset);

            if (!rom.IsRomAddress(value)) continue;

            if (!index.TryGetValue(value, out List<int>? at)) index[value] = at = [];

            at.Add(offset);
        }

        return index.ToDictionary(p => p.Key, p => (IReadOnlyList<int>)p.Value);
    }

    /// <summary>
    /// How often a candidate argument width carries a read on into an address something names.
    /// <para>
    /// <b>The one signal in this file that says where a script stops.</b> A block with its own
    /// pointer is a script somebody jumps to, and you do not fall into one — so a width whose
    /// next command lands on such an address has almost always eaten the <c>end</c> in front of
    /// it and is now reading the neighbouring script as though it were this one.
    /// </para>
    /// <para>
    /// Every continuation test this project has preferred the longer width for exactly that
    /// reason: the longer width skips whatever the reader cannot yet handle and lands on
    /// something that parses beautifully and is not there. 0xD0 — fifty-one stopped blocks,
    /// more than the next three commands together — went that way, and this is what caught it.
    /// </para>
    /// <para>
    /// Lives here rather than in whoever is printing, because it is a rule about telling two
    /// cases apart and this project has now three times written one of those into the reporting
    /// layer, which has no tests.
    /// </para>
    /// </summary>
    public static double ReadsOnIntoSomebodyElses(
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index, IReadOnlyList<int> sites, int width)
    {
        if (sites.Count == 0) return 0;

        return sites.Count(at => index.ContainsKey(Rom.BaseAddress + (uint)(at + 1 + width)))
            / (double)sites.Count;
    }

    /// <summary>
    /// Everything that names this address, or any address in the bytes just above it.
    /// <para>
    /// The slack is the point. A script jumped into at its first command is named exactly; a
    /// command in the middle of a block is named by nothing at all, and the block that contains
    /// it is named a few dozen bytes above. Asking only for the exact address answers "no"
    /// correctly and uselessly.
    /// </para>
    /// </summary>
    public static IReadOnlyList<NamesIt> WhoNames(
        Rom rom, IReadOnlyDictionary<uint, IReadOnlyList<int>> index, uint address, int slack = 0)
    {
        var found = new List<NamesIt>();

        for (uint target = address - (uint)slack; target <= address; target++)
        {
            if (!index.TryGetValue(target, out IReadOnlyList<int>? offsets)) continue;

            foreach (int offset in offsets) found.Add(new NamesIt(offset, target, OpcodeFor(rom, offset)));
        }

        return [.. found.OrderBy(n => n.Offset)];
    }

    /// <summary>
    /// Which command owns a pointer sitting at this offset, or zero when none does.
    /// <para>
    /// Read from the bytes in front of it rather than guessed. <c>call</c> and <c>goto</c> put
    /// their pointer immediately after the opcode; the conditional pair put a condition byte in
    /// between; <c>loadpointer</c> puts a bank byte there and its pointer is text rather than
    /// script, which is worth telling apart rather than counting as a way in.
    /// </para>
    /// </summary>
    private static byte OpcodeFor(Rom rom, int offset)
    {
        if (offset >= 1 && rom.ReadU8(offset - 1) is ScriptCommands.Call or ScriptCommands.Goto)
            return rom.ReadU8(offset - 1);

        if (offset >= 2
            && rom.ReadU8(offset - 2) is ScriptCommands.GotoIf or ScriptCommands.CallIf
                or ScriptCommands.LoadPointer)
        {
            return rom.ReadU8(offset - 2);
        }

        return 0;
    }

    /// <summary>
    /// True when the bytes from here decode as commands and finish like a script.
    /// <para>
    /// The same test <see cref="ScriptReader"/> uses to decide whether a pointer out of a fight
    /// leads to a script, applied to a byte scan's hits for the same reason: a hit in the
    /// middle of somebody's argument does not carry on into commands.
    /// </para>
    /// </summary>
    private static bool ReadsAsAScript(Rom rom, uint address)
    {
        List<ScriptCommand> commands = ScriptReader.Read(rom, address);

        return commands.Count > 0
            && commands[^1].Code is ScriptCommands.End or ScriptCommands.Return or ScriptCommands.Goto;
    }
}

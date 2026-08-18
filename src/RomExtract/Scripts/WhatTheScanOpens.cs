using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// How many times the map scan reads each command, and how many BYTE POSITIONS those reads are.
/// </summary>
/// <remarks>
/// <para>
/// <b>The error bar on every map-scan number in this project, in one table.</b> The scan walks
/// every script the maps hang off anything and follows calls, so a block shared by nineteen
/// Pokémon Centres is decoded nineteen times. Every sweep built on it counts reads unless it was
/// written to do otherwise, and 220 and 223 found that two of them were not.
/// </para>
/// <para>
/// Those two were corrected one at a time by hand. This asks the question of all of them at once:
/// for each command code, how far apart the two numbers are. A code whose reads and places are
/// equal has nothing to correct anywhere; a code read twenty-nine times per address has it
/// waiting in every instrument that counts it.
/// </para>
/// </remarks>
public static class WhatTheScanOpens
{
    /// <param name="Code">The command's opcode.</param>
    /// <param name="Reads">How many times the scan decodes it.</param>
    /// <param name="Places">How many distinct byte positions those reads are.</param>
    /// <param name="Scripts">How many script entries reach it.</param>
    /// <param name="Maps">How many maps those entries are on.</param>
    public sealed record ACode(byte Code, int Reads, int Places, int Scripts, int Maps)
    {
        /// <summary>
        /// Reads per byte position. <b>One means nothing anywhere counted this code wrongly;
        /// anything above it is how wrong.</b>
        /// </summary>
        public double Over => Places == 0 ? 0 : (double)Reads / Places;
    }

    /// <param name="Entries">Script entries the maps hang off people, triggers and signs.</param>
    /// <param name="Addresses">How many distinct addresses those entries point at.</param>
    /// <param name="Reads">Commands decoded, counting a shared block once per entry that reaches it.</param>
    /// <param name="Places">How many distinct byte positions the scan decodes at all.</param>
    public sealed record Overall(int Entries, int Addresses, int Reads, int Places);

    /// <summary>
    /// Every command the scan decodes, by code, in reads and in places.
    /// </summary>
    public static (Overall Whole, List<ACode> ByCode) Of(Rom rom, MapLibrary library)
    {
        var reads = new Dictionary<byte, int>();
        var places = new Dictionary<byte, HashSet<int>>();
        var scripts = new Dictionary<byte, int>();
        var maps = new Dictionary<byte, HashSet<string>>();
        var entries = 0;
        var addresses = new HashSet<uint>();
        var everywhere = new HashSet<int>();
        var read = 0;

        foreach ((string mapId, string _, uint address) in library.EveryScript())
        {
            entries++;
            addresses.Add(address);

            var here = new HashSet<byte>();

            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, address))
            {
                read++;
                everywhere.Add(command.Offset);

                reads[command.Code] = reads.GetValueOrDefault(command.Code) + 1;

                if (!places.TryGetValue(command.Code, out HashSet<int>? at)) places[command.Code] = at = [];

                at.Add(command.Offset);

                if (!maps.TryGetValue(command.Code, out HashSet<string>? on)) maps[command.Code] = on = [];

                on.Add(mapId);

                // Once per entry, however many times the entry reads this code — the entry count
                // is about how many doors lead here, not about how long the block is.
                if (here.Add(command.Code))
                    scripts[command.Code] = scripts.GetValueOrDefault(command.Code) + 1;
            }
        }

        List<ACode> byCode =
        [
            .. reads.Keys
                .Select(code => new ACode(
                    code,
                    reads[code],
                    places[code].Count,
                    scripts.GetValueOrDefault(code),
                    maps[code].Count))
                .OrderByDescending(c => c.Over)
                .ThenByDescending(c => c.Reads),
        ];

        return (new Overall(entries, addresses.Count, read, everywhere.Count), byCode);
    }
}

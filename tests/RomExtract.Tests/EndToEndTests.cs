using System.Text.Json;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Drives the actual command-line entry point against a synthetic cartridge on disk,
/// so argument parsing, file IO, and output formats are covered rather than just the
/// library internals.
/// </summary>
public class EndToEndTests : IDisposable
{
    private readonly string _workDirectory =
        Path.Combine(Path.GetTempPath(), "pokemmo-e2e-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _romPath;

    public EndToEndTests()
    {
        Directory.CreateDirectory(_workDirectory);
        _romPath = Path.Combine(_workDirectory, "synthetic.gba");
        File.WriteAllBytes(_romPath, new SyntheticRom().Bytes);
    }

    public void Dispose()
    {
        try { Directory.Delete(_workDirectory, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private string OutputDirectory => Path.Combine(_workDirectory, "out");

    private int RunCli(params string[] extraArgs) =>
        Tools.RomDump.Program.Main([_romPath, "--out", OutputDirectory, .. extraArgs]);

    [Fact]
    public void DumpsDataAndSpritesFromACartridgeOnDisk()
    {
        int exitCode = RunCli("--species", "1");

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(OutputDirectory, "species.json")));
        Assert.True(File.Exists(Path.Combine(OutputDirectory, "tables.json")));
        Assert.True(File.Exists(Path.Combine(OutputDirectory, "sprites", "001.png")));
    }

    [Fact]
    public void WritesSpeciesJsonThatDeserialisesWithReadableEnums()
    {
        RunCli("--no-sprites");

        using JsonDocument doc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(OutputDirectory, "species.json")));

        JsonElement bulbasaur = doc.RootElement[SyntheticRom.TestSpecies];

        Assert.Equal("BULBASAUR", bulbasaur.GetProperty("Name").GetString());
        Assert.Equal(45, bulbasaur.GetProperty("BaseHp").GetInt32());
        Assert.Equal("Grass", bulbasaur.GetProperty("Type1").GetString());
        Assert.Equal("Poison", bulbasaur.GetProperty("Type2").GetString());
        Assert.Equal("MediumSlow", bulbasaur.GetProperty("GrowthRate").GetString());
    }

    [Fact]
    public void WritesATableReportRecordingWhereEverythingWasFound()
    {
        RunCli("--no-sprites");

        using JsonDocument doc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(OutputDirectory, "tables.json")));

        JsonElement tables = doc.RootElement.GetProperty("tables");
        Assert.Equal(6, tables.GetArrayLength());

        JsonElement baseStats = tables.EnumerateArray()
            .Single(t => t.GetProperty("Name").GetString() == "BaseStats");

        Assert.Equal(
            $"0x{Rom.BaseAddress + SyntheticRom.BaseStatsOffset:X8}",
            baseStats.GetProperty("address").GetString());

        Assert.False(doc.RootElement.GetProperty("cartridge").GetProperty("Sha1IsKnown").GetBoolean());
    }

    [Fact]
    public void RendersShinyAndBackSpritesToDistinctFiles()
    {
        RunCli("--species", "1", "--shiny");
        RunCli("--species", "1", "--back");

        string shiny = Path.Combine(OutputDirectory, "sprites", "001_shiny.png");
        string back = Path.Combine(OutputDirectory, "sprites", "001_back.png");

        Assert.True(File.Exists(shiny));
        Assert.True(File.Exists(back));
        Assert.NotEqual(File.ReadAllBytes(shiny), File.ReadAllBytes(back));
    }

    [Fact]
    public void SkipsSpriteRenderingOnRequest()
    {
        RunCli("--no-sprites");
        Assert.False(Directory.Exists(Path.Combine(OutputDirectory, "sprites")));
    }

    [Fact]
    public void ReportsAnErrorForAMissingRomInsteadOfCrashing()
    {
        int exitCode = Tools.RomDump.Program.Main([Path.Combine(_workDirectory, "nope.gba")]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void ReportsAnErrorForAnUnknownOption()
    {
        Assert.Equal(1, Tools.RomDump.Program.Main([_romPath, "--wat"]));
    }

    [Fact]
    public void ExitsWithADistinctCodeWhenNoTablesAreFound()
    {
        string junkPath = Path.Combine(_workDirectory, "junk.gba");
        var junk = new byte[512 * 1024];
        new Random(42).NextBytes(junk);
        File.WriteAllBytes(junkPath, junk);

        Assert.Equal(2, Tools.RomDump.Program.Main([junkPath, "--out", OutputDirectory]));
    }

    [Fact]
    public void PrintsUsageWithoutArguments()
    {
        Assert.Equal(1, Tools.RomDump.Program.Main([]));
        Assert.Equal(0, Tools.RomDump.Program.Main(["--help"]));
    }
}

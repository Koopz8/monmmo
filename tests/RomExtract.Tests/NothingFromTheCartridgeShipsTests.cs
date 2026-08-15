using System.Diagnostics;
using System.Text;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The project's most important rule, enforced rather than promised.
/// <para>
/// The client ships no cartridge data. Every player supplies their own file and extraction
/// happens on their machine — that is the whole answer to the legal question, and it is
/// worth exactly as much as the weakest thing holding it up.
/// </para>
/// <para>
/// Until now the thing holding it up was <c>.gitignore</c>, and that file's own comment
/// says what that was worth: the exporter's two outputs "were only ever kept out of the
/// repository by nobody having put one in the root, which is not the same as a rule. One
/// got committed the moment somebody did." A rule nobody can break by accident is a rule;
/// a habit is not.
/// </para>
/// <para>
/// So this asks git what is actually tracked, which is the only question that matters —
/// what is in the working tree is the player's own business, and a cartridge sitting
/// beside the checkout is exactly where a cartridge is supposed to be.
/// </para>
/// </summary>
public class NothingFromTheCartridgeShipsTests
{
    /// <summary>Images, saves, and the two files the exporter writes.</summary>
    private static readonly string[] Extensions = [".gba", ".gbc", ".gb", ".nds", ".sav", ".srm"];

    private static readonly string[] Names = ["world.dat", "rules.dat", "players.db"];

    /// <summary>
    /// The first bytes of the logo every cartridge of this family carries at offset four.
    /// <para>
    /// Here because an extension list is a rule about filenames and the failure it misses
    /// is the one that would actually happen: an image renamed to something that looks
    /// harmless. This reads the bytes instead, so what is checked is what a file
    /// <em>is</em>.
    /// </para>
    /// </summary>
    private static readonly byte[] CartridgeLogo = [0x24, 0xFF, 0xAE, 0x51, 0x69, 0x9A, 0xA2, 0x21];

    private const int LogoAt = 4;

    [Fact]
    public void NothingTrackedIsFromACartridge()
    {
        DirectoryInfo root = Repository();

        var wrong = new List<string>();

        foreach (string tracked in Tracked(root))
        {
            string full = Path.Combine(root.FullName, tracked);

            if (Extensions.Contains(Path.GetExtension(tracked), StringComparer.OrdinalIgnoreCase))
            {
                wrong.Add($"{tracked} — a cartridge image or save");
                continue;
            }

            if (Names.Contains(Path.GetFileName(tracked), StringComparer.OrdinalIgnoreCase))
            {
                wrong.Add($"{tracked} — derived from somebody's cartridge");
                continue;
            }

            if (LooksLikeACartridge(full)) wrong.Add($"{tracked} — carries a cartridge header");
        }

        Assert.Empty(wrong);
    }

    /// <summary>
    /// And the ignore rules still name every shape, so the next one to arrive is refused
    /// before anybody has to notice it.
    /// <para>
    /// The test above catches a file that got committed; this catches the rule being
    /// deleted, which is what makes the next one possible. They fail in different orders
    /// and both are worth having.
    /// </para>
    /// </summary>
    [Fact]
    public void AndTheIgnoreRulesStillNameEveryShape()
    {
        string ignore = File.ReadAllText(Path.Combine(Repository().FullName, ".gitignore"));

        var missing = new List<string>();

        foreach (string extension in Extensions)
            if (!ignore.Contains($"*{extension}", StringComparison.OrdinalIgnoreCase))
                missing.Add($"*{extension}");

        foreach (string name in Names)
            if (!ignore.Contains(name, StringComparison.OrdinalIgnoreCase))
                missing.Add(name);

        Assert.Empty(missing);
    }

    private static bool LooksLikeACartridge(string path)
    {
        try
        {
            using FileStream file = File.OpenRead(path);

            if (file.Length < LogoAt + CartridgeLogo.Length) return false;

            var head = new byte[LogoAt + CartridgeLogo.Length];

            if (file.Read(head, 0, head.Length) != head.Length) return false;

            return head.AsSpan(LogoAt).SequenceEqual(CartridgeLogo);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable is not the same as forbidden, and a test that cannot open a file
            // has not found anything out about it.
            return false;
        }
    }

    /// <summary>
    /// Everything git is actually tracking, which is a different set from everything on
    /// disk and is the only one this rule is about.
    /// <para>
    /// Asked of git rather than worked out by re-implementing <c>.gitignore</c>, because a
    /// second implementation of those rules would be a second thing that can disagree with
    /// the first — and the one that decides what ships is git's.
    /// </para>
    /// </summary>
    private static IEnumerable<string> Tracked(DirectoryInfo root)
    {
        var git = new ProcessStartInfo("git", "ls-files")
        {
            WorkingDirectory = root.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        using Process? running = Process.Start(git)
            ?? throw new InvalidOperationException("could not run git, and this guardrail must not pass without it");

        string listed = running.StandardOutput.ReadToEnd();

        running.WaitForExit();

        // A guardrail that goes quiet when its instrument breaks is worse than no
        // guardrail, because it reads as a pass. The same argument the repository finder
        // below makes, one layer down.
        if (running.ExitCode != 0)
            throw new InvalidOperationException($"git ls-files failed in {root.FullName}");

        string[] files = listed.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (files.Length == 0)
            throw new InvalidOperationException($"git tracks nothing in {root.FullName} — this guardrail read the wrong place");

        return files.Select(f => f.Trim().Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// The checkout this test is running out of, found the way the other guardrail in this
    /// suite finds it — by looking for a file that has to be there.
    /// </summary>
    private static DirectoryInfo Repository()
    {
        for (DirectoryInfo? at = new(AppContext.BaseDirectory); at is not null; at = at.Parent)
        {
            if (File.Exists(Path.Combine(at.FullName, ".gitignore")) &&
                Directory.Exists(Path.Combine(at.FullName, "src")))
            {
                return at;
            }
        }

        throw new InvalidOperationException(
            $"no checkout above {AppContext.BaseDirectory} — this guardrail reads the repository and " +
            "must not pass without it");
    }
}

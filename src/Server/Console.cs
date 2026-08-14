namespace PokeMmo.Server;

/// <summary>
/// One line typed into the operator console, taken apart.
/// <para>
/// A separate type so the parsing can be tested without a world, and so the one thing
/// that matters about a console — that it is a request and not an instruction — is
/// visible in the shape: this says what was asked for and decides nothing.
/// </para>
/// </summary>
public sealed record ConsoleLine(string Verb, IReadOnlyList<string> Words)
{
    public static ConsoleLine Of(string text)
    {
        string[] parts = text.Trim().TrimStart('/').Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length == 0
            ? new ConsoleLine("", [])
            : new ConsoleLine(parts[0].ToLowerInvariant(), parts[1..]);
    }

    public string Word(int at) => at < Words.Count ? Words[at] : "";

    /// <summary>
    /// A number, in decimal or hexadecimal.
    /// <para>
    /// Both, because half of what a console is for here is flags and variables, and this
    /// cartridge's are written 0x4055 everywhere they appear — in its own scripts, in
    /// this project's comments, and in every note anybody has taken about them. Making
    /// somebody convert that to 16469 by hand is asking them to introduce a typo.
    /// </para>
    /// </summary>
    public static int? Number(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        bool hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        string digits = hex ? text[2..] : text;

        return hex
            ? int.TryParse(digits, System.Globalization.NumberStyles.HexNumber, null, out int read) ? read : null
            : int.TryParse(digits, out int plain) ? plain : null;
    }

    public int? Number(int at) => Number(Word(at));
}

/// <summary>What the console knows how to do, for its own help text.</summary>
public static class ConsoleHelp
{
    public static readonly IReadOnlyList<string> Lines =
    [
        "/where                     what map and square you are on",
        "/tp <map> <x> <y>          go somewhere, e.g. /tp 3.1 25 20",
        "/give <species> <level>    one more in the party",
        "/item <id> [count]         something for the bag",
        "/flag <id> [on|off]        set or clear a story flag, e.g. /flag 0x082C",
        "/var <id> <value>          write a story variable, e.g. /var 0x4055 3",
        "/read <id>                 what a variable holds",
        "/heal                      everybody back on their feet",
        "/hurt [hp]                 everybody down to that much, 1 by default",
        "/ail <condition>           everybody poisoned, burned, asleep — or none",
        "/money <amount>            set it",
        "/forget                    every flag and variable, gone",
        "/help                      this",
    ];
}

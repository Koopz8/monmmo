using System;
using System.IO;
using System.Text.Json;

namespace PokeMmo.Client;

/// <summary>
/// Where the player's cartridge is and which map to open.
/// <para>
/// The path is configuration rather than something shipped: the cartridge belongs to
/// the player, stays on their machine, and never enters this repository. Both the
/// settings file and any image it points at are ignored by git.
/// </para>
/// </summary>
public sealed class ClientSettings
{
    public const string FileName = "client.json";

    /// <summary>Absolute path to the player's own cartridge image.</summary>
    public string RomPath { get; set; } = "";

    /// <summary>Which map to open, matched against location names.</summary>
    public string MapName { get; set; } = "PALLET TOWN";

    /// <summary>Optional exact address, as <c>bank.map</c>. Takes precedence over the name.</summary>
    public string? MapAddress { get; set; }

    /// <summary>Server to join, as <c>host</c> or <c>host:port</c>. Empty plays alone.</summary>
    public string Server { get; set; } = "";

    /// <summary>Name other players see when playing alone.</summary>
    public string PlayerName { get; set; } = "Player";

    /// <summary>
    /// The account name to offer at the login screen. Remembered; the password is
    /// not, and never will be — a client that stores one is a client that can have it
    /// taken off the machine.
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// The creature you battle with. A placeholder until there is a party system —
    /// species 1 is the first starter.
    /// </summary>
    public int StarterSpecies { get; set; } = 1;

    public int StarterLevel { get; set; } = 5;

    /// <summary>
    /// Which of the two sets of words this character reads.
    /// <para>
    /// Here rather than in the save, and that is the interesting part. The server has
    /// never seen a line of this game's text and has no opinion about any of it; the
    /// choice only ever affects which arm of a fork the client reads, so it lives beside
    /// the cartridge path along with everything else that is about text.
    /// </para>
    /// <para>
    /// One command asks: 0xA0, which takes nothing and answers into the result variable.
    /// The arms after it are "Waiter"/"Waitress", "little brother"/"little sister", "All
    /// boys leave home someday"/"All girls dream of traveling" — seven scripts on six
    /// maps, and the zero arm is the first of each pair at every one of them.
    /// </para>
    /// </summary>
    public bool Girl { get; set; }

    /// <summary>Balls carried. A placeholder for a bag, which does not exist yet.</summary>
    public int Balls { get; set; } = 20;

    public bool IsUsable => !string.IsNullOrWhiteSpace(RomPath) && File.Exists(RomPath);

    /// <summary>
    /// Reads settings from the file beside the project, then lets command-line
    /// arguments override them: <c>--rom &lt;path&gt; --map &lt;name&gt; --at &lt;bank.map&gt;</c>.
    /// </summary>
    public static ClientSettings Load(string projectDirectory, string[] commandLineArgs)
    {
        ClientSettings settings = ReadFile(Path.Combine(projectDirectory, FileName));

        for (int i = 0; i < commandLineArgs.Length - 1; i++)
        {
            switch (commandLineArgs[i])
            {
                case "--rom":
                    settings.RomPath = commandLineArgs[i + 1];
                    break;
                case "--map":
                    settings.MapName = commandLineArgs[i + 1];
                    break;
                case "--at":
                    settings.MapAddress = commandLineArgs[i + 1];
                    break;
                case "--server":
                    settings.Server = commandLineArgs[i + 1];
                    break;
                case "--name":
                    settings.PlayerName = commandLineArgs[i + 1];
                    break;
                case "--girl":
                    settings.Girl = commandLineArgs[i + 1] is not ("false" or "0" or "no");
                    break;
                case "--starter":
                    if (int.TryParse(commandLineArgs[i + 1], out int starter))
                        settings.StarterSpecies = starter;
                    break;
                case "--starter-level":
                    if (int.TryParse(commandLineArgs[i + 1], out int starterLevel))
                        settings.StarterLevel = starterLevel;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(settings.RomPath))
            settings.RomPath = Environment.GetEnvironmentVariable("MONMMO_ROM") ?? "";

        return settings;
    }

    private static ClientSettings ReadFile(string path)
    {
        if (!File.Exists(path)) return new ClientSettings();

        try
        {
            return JsonSerializer.Deserialize<ClientSettings>(File.ReadAllText(path)) ?? new ClientSettings();
        }
        catch (JsonException)
        {
            // A malformed settings file should not stop the game starting; the
            // on-screen message will explain what is missing.
            return new ClientSettings();
        }
    }

    /// <summary>
    /// Stores the account name for next time, leaving every other setting alone.
    /// <para>
    /// The file is re-read rather than this instance written out, because this one may
    /// carry command-line overrides that were meant for a single run.
    /// </para>
    /// </summary>
    public static void RememberUsername(string projectDirectory, string username)
    {
        string path = Path.Combine(projectDirectory, FileName);

        ClientSettings onDisk = ReadFile(path);
        if (onDisk.Username == username) return;

        onDisk.Username = username;

        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(onDisk, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));
        }
        catch (IOException)
        {
            // Not being able to remember a name is not worth interrupting a game for.
        }
    }

    /// <summary>Writes a template so the player has something to fill in.</summary>
    public static void WriteTemplate(string projectDirectory)
    {
        string path = Path.Combine(projectDirectory, FileName);
        if (File.Exists(path)) return;

        var template = new ClientSettings { RomPath = "", MapName = "PALLET TOWN" };

        File.WriteAllText(path, JsonSerializer.Serialize(template, new JsonSerializerOptions
        {
            WriteIndented = true,
        }));
    }
}

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

    /// <summary>Name other players see.</summary>
    public string PlayerName { get; set; } = "Player";

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

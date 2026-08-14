namespace PokeMmo.Core.Data;

/// <summary>
/// One thing a species can turn into, and what it takes.
/// <para>
/// Numbers only, like everything else in the rules file. <see cref="Method"/> and
/// <see cref="Parameter"/> are the cartridge's own — the server has no idea that method
/// seven means a stone or that item ninety-six is the thunder one, and does not need
/// one. What it knows is which method means "at a level", because the export worked
/// that out and wrote it down beside these.
/// </para>
/// </summary>
public sealed record Evolution(int Species, int Method, int Parameter, int Into);

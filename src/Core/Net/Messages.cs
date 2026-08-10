using System.Text.Json.Serialization;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;

namespace PokeMmo.Core.Net;

/// <summary>
/// Everything that can travel between client and server.
/// <para>
/// These types live in <c>Core</c> for the same reason the movement rules do: a
/// protocol defined twice is a protocol that will eventually disagree with itself.
/// One definition, referenced by both sides.
/// </para>
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "t")]
[JsonDerivedType(typeof(RegisterRequest), "register")]
[JsonDerivedType(typeof(LoginRequest), "login")]
[JsonDerivedType(typeof(SaveRequest), "save")]
[JsonDerivedType(typeof(MoveRequest), "move")]
[JsonDerivedType(typeof(Welcome), "welcome")]
[JsonDerivedType(typeof(AuthFailed), "authfailed")]
[JsonDerivedType(typeof(PlayerAppeared), "appeared")]
[JsonDerivedType(typeof(PlayerMoved), "moved")]
[JsonDerivedType(typeof(PlayerLeft), "left")]
[JsonDerivedType(typeof(MoveRejected), "rejected")]
[JsonDerivedType(typeof(WildEncounterStarted), "encounter")]
[JsonDerivedType(typeof(Rejected), "error")]
public abstract record NetMessage;

// --- client to server --------------------------------------------------------

/// <summary>Creates an account and enters the world with a fresh character.</summary>
public sealed record RegisterRequest(string Username, string Password) : NetMessage;

/// <summary>
/// Enters the world as an existing account.
/// <para>
/// Logging in and joining are one step rather than two. A connection that has
/// authenticated but not yet joined is a state with no purpose, and every state a
/// protocol has is a state its server has to handle correctly.
/// </para>
/// </summary>
public sealed record LoginRequest(string Username, string Password) : NetMessage;

/// <summary>Asks to step one square. The server decides whether it happens.</summary>
public sealed record MoveRequest(Direction Direction) : NetMessage;

/// <summary>
/// Reports the party after a battle, so the server can store it.
/// <para>
/// This is a temporary trust gap and worth naming: battles resolve on the client, so
/// the client is telling the server what it caught. It is fine while the only player
/// is the person running the server, and it has to close before anyone else plays.
/// Closing it means resolving battles server-side, which needs base stats and catch
/// rates the server does not have — a species export, the same arrangement as the
/// world file.
/// </para>
/// </summary>
public sealed record SaveRequest(int Balls, IReadOnlyList<SavedMon> Party) : NetMessage;

// --- server to client --------------------------------------------------------

/// <summary>Accepts a login and hands back everything the character was left with.</summary>
public sealed record Welcome(
    int PlayerId,
    string MapId,
    int X,
    int Y,
    Direction Facing,
    int Balls,
    IReadOnlyList<SavedMon> Party) : NetMessage;

/// <summary>
/// The credentials were not accepted. Deliberately vague about which half was wrong,
/// so this cannot be used to find out which usernames exist.
/// </summary>
public sealed record AuthFailed(string Reason) : NetMessage;

/// <summary>Another player is now visible — sent on join, and for everyone already present.</summary>
public sealed record PlayerAppeared(int PlayerId, string Name, int X, int Y, Direction Facing) : NetMessage;

/// <summary>A player stepped. Sent to everyone, including the player who moved.</summary>
public sealed record PlayerMoved(int PlayerId, int X, int Y, Direction Facing) : NetMessage;

public sealed record PlayerLeft(int PlayerId) : NetMessage;

/// <summary>
/// The server did not allow a step, and here is where the player actually is.
/// <para>
/// Because both sides run the same movement code this should be rare — it exists for
/// the cases prediction cannot cover, like moving faster than the server allows.
/// </para>
/// </summary>
public sealed record MoveRejected(int X, int Y, Direction Facing, string Reason) : NetMessage;

/// <summary>
/// Something appeared in the grass. Sent only to the player who stepped on it.
/// <para>
/// The seed travels with it so the client can resolve the battle locally and reach
/// the same result the server will — the same replay arrangement used for combat.
/// </para>
/// </summary>
public sealed record WildEncounterStarted(int Species, int Level, uint Seed) : NetMessage;

/// <summary>The request could not be honoured at all.</summary>
public sealed record Rejected(string Reason) : NetMessage;

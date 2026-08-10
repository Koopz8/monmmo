using System.Text.Json.Serialization;
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
[JsonDerivedType(typeof(JoinRequest), "join")]
[JsonDerivedType(typeof(MoveRequest), "move")]
[JsonDerivedType(typeof(Welcome), "welcome")]
[JsonDerivedType(typeof(PlayerAppeared), "appeared")]
[JsonDerivedType(typeof(PlayerMoved), "moved")]
[JsonDerivedType(typeof(PlayerLeft), "left")]
[JsonDerivedType(typeof(MoveRejected), "rejected")]
[JsonDerivedType(typeof(WildEncounterStarted), "encounter")]
[JsonDerivedType(typeof(Rejected), "error")]
public abstract record NetMessage;

// --- client to server --------------------------------------------------------

/// <summary>Asks to enter the world.</summary>
public sealed record JoinRequest(string Name) : NetMessage;

/// <summary>Asks to step one square. The server decides whether it happens.</summary>
public sealed record MoveRequest(Direction Direction) : NetMessage;

// --- server to client --------------------------------------------------------

/// <summary>Accepts a join and says where the player now stands.</summary>
public sealed record Welcome(int PlayerId, string MapId, int X, int Y, Direction Facing) : NetMessage;

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

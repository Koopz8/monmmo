using System.Text.Json.Serialization;
using PokeMmo.Core.Battle;
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
[JsonDerivedType(typeof(BattleTurn), "turn")]
[JsonDerivedType(typeof(MoveRequest), "move")]
[JsonDerivedType(typeof(TalkRequest), "talk")]
[JsonDerivedType(typeof(TalkFinished), "talkdone")]
[JsonDerivedType(typeof(BuyRequest), "buy")]
[JsonDerivedType(typeof(SellRequest), "sell")]
[JsonDerivedType(typeof(ShopOpened), "shop")]
[JsonDerivedType(typeof(ShopUpdated), "shopupdate")]
[JsonDerivedType(typeof(Welcome), "welcome")]
[JsonDerivedType(typeof(AuthFailed), "authfailed")]
[JsonDerivedType(typeof(MapChanged), "mapchanged")]
[JsonDerivedType(typeof(ObjectsPlaced), "objects")]
[JsonDerivedType(typeof(ObjectMoved), "objectmoved")]
[JsonDerivedType(typeof(PlayerAppeared), "appeared")]
[JsonDerivedType(typeof(PlayerMoved), "moved")]
[JsonDerivedType(typeof(PlayerLeft), "left")]
[JsonDerivedType(typeof(MoveRejected), "rejected")]
[JsonDerivedType(typeof(BattleStarted), "battlestart")]
[JsonDerivedType(typeof(BattleUpdate), "battleupdate")]
[JsonDerivedType(typeof(BattlerSentOut), "sentout")]
[JsonDerivedType(typeof(BattleFinished), "battleend")]
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
/// The player has started talking to somebody, and would like them to stand still.
/// <para>
/// The script itself is not asked for and could not be answered: the server has no
/// cartridge and so has no idea what anybody says. All this buys is a person who is
/// still there at the end of their own sentence — they wander every second or so
/// otherwise, and a shopkeeper strolling off mid-conversation is the whole reason this
/// message exists.
/// </para>
/// </summary>
public sealed record TalkRequest(int LocalId) : NetMessage;

/// <summary>The text box is closed; whoever was held may carry on. Also shuts a shop.</summary>
public sealed record TalkFinished : NetMessage;

/// <summary>
/// Buy some of one thing from the shop that is open.
/// <para>
/// The item and the count, and no price. A request carrying a price is a request a
/// client can lie in — what it costs is the server's to look up.
/// </para>
/// </summary>
public sealed record BuyRequest(int ItemId, int Count) : NetMessage;

/// <summary>Sell some of one thing to the shop that is open.</summary>
public sealed record SellRequest(int ItemId, int Count) : NetMessage;

/// <summary>
/// What the player chose to do this turn.
/// <para>
/// A request, not a result. The server holds the battle, rolls the dice and decides
/// what happened — this says only what was asked for.
/// </para>
/// </summary>
public sealed record BattleTurn(BattleAction Action) : NetMessage;

// --- server to client --------------------------------------------------------

/// <summary>Accepts a login and hands back everything the character was left with.</summary>
public sealed record Welcome(
    int PlayerId,
    string MapId,
    int X,
    int Y,
    Direction Facing,
    int Money,
    IReadOnlyList<BagEntry> Bag,
    IReadOnlyList<SavedMon> Party) : NetMessage;

/// <summary>
/// The player is now on a different map, through a door or off an edge.
/// <para>
/// Sent only to the player who moved. Everyone else sees them leave one map and
/// appear on another, which is the same pair of messages a disconnect and a join
/// would produce — so a client watching other players needs no new case at all.
/// </para>
/// </summary>
public sealed record MapChanged(string MapId, int X, int Y, Direction Facing) : NetMessage;

/// <summary>
/// The credentials were not accepted. Deliberately vague about which half was wrong,
/// so this cannot be used to find out which usernames exist.
/// </summary>
public sealed record AuthFailed(string Reason) : NetMessage;

/// <summary>
/// Somebody standing on a map, as the server has them right now.
/// <para>
/// Sent rather than read from the cartridge, which is a change from when they stood
/// still. Once they move, where they are is a fact about the running world rather than
/// about the image on anyone's disk, and two machines deciding it independently would
/// disagree within seconds.
/// </para>
/// </summary>
public sealed record ObjectView(int LocalId, int GraphicsId, int X, int Y, Direction Facing);

/// <summary>Everyone standing on the map a player has just arrived on.</summary>
public sealed record ObjectsPlaced(IReadOnlyList<ObjectView> Objects) : NetMessage;

/// <summary>One of them turned or took a step.</summary>
public sealed record ObjectMoved(int LocalId, int X, int Y, Direction Facing) : NetMessage;

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
/// One side of a battle, as the other end needs to draw it.
/// <para>
/// Numbers only, like everything else the server sends. The species index becomes a
/// name and a sprite on the machine that has a cartridge.
/// </para>
/// </summary>
public sealed record BattlerView(
    int Species,
    int Level,
    string? Nickname,
    int CurrentHp,
    int MaxHp,
    StatusCondition Status,
    IReadOnlyList<int> Moves);

/// <summary>
/// A battle has begun. Sent only to the player in it.
/// <para>
/// <paramref name="TrainerId"/> is null for something in the grass and set when a
/// person started it. The id is all that is sent — the client has a cartridge and
/// turns it into a name and a class, exactly as it does for a species.
/// </para>
/// </summary>
public sealed record BattleStarted(
    BattlerView You,
    BattlerView Opponent,
    IReadOnlyList<BagEntry> Balls,
    IReadOnlyList<BagEntry> Medicine,
    int? TrainerId = null) : NetMessage;

/// <summary>
/// One side has sent out somebody new.
/// <para>
/// Its own message rather than a field on <see cref="BattleUpdate"/>, because it
/// happens between turns rather than during one: whoever fainted did so as part of the
/// turn just reported, and this is what comes next.
/// </para>
/// </summary>
public sealed record BattlerSentOut(Side Side, BattlerView Battler) : NetMessage;

/// <summary>
/// What happened this turn, and where both sides now stand.
/// <para>
/// Health is sent alongside the events rather than left to be derived from them. The
/// events are what the player reads; these are what the bars draw from, and a client
/// that has to reconstruct state by replaying a narrative will eventually disagree
/// with the server about it.
/// </para>
/// </summary>
public sealed record BattleUpdate(
    IReadOnlyList<BattleEvent> Events,
    int YourHp,
    int OpponentHp,
    IReadOnlyList<BagEntry> Balls,
    IReadOnlyList<BagEntry> Medicine) : NetMessage;

/// <summary>
/// The battle is over. Carries the party back because it may have just grown.
/// </summary>
public sealed record BattleFinished(
    Side? Winner,
    bool Caught,
    int Money,
    int Prize,
    IReadOnlyList<BagEntry> Balls,
    IReadOnlyList<SavedMon> Party) : NetMessage;

/// <summary>One line of a shop's stock: what it is and what it costs today.</summary>
public sealed record ShopEntry(int ItemId, int Price);

/// <summary>
/// A shop is open. Sent only to the player who opened it.
/// <para>
/// The whole bag comes with it rather than only the pockets a shop touches, because
/// selling needs to show everything sellable and a shop that could only see balls would
/// be a strange shop.
/// </para>
/// </summary>
public sealed record ShopOpened(
    IReadOnlyList<ShopEntry> Stock,
    int Money,
    IReadOnlyList<BagEntry> Bag) : NetMessage;

/// <summary>
/// What the money and the bag are after something was bought or sold.
/// <para>
/// Sent instead of a yes or no. A refusal and a purchase differ only in what these two
/// numbers become, and a client that had to work out which happened would eventually
/// work it out wrongly.
/// </para>
/// </summary>
public sealed record ShopUpdated(int Money, IReadOnlyList<BagEntry> Bag, string Message) : NetMessage;

/// <summary>The request could not be honoured at all.</summary>
public sealed record Rejected(string Reason) : NetMessage;

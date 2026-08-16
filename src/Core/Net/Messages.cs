using System.Text.Json.Serialization;
using PokeMmo.Core.Battle;
using PokeMmo.Core.Cosmetics;
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
[JsonDerivedType(typeof(GoToRequest), "goto")]
[JsonDerivedType(typeof(ScriptRan), "scriptran")]
[JsonDerivedType(typeof(FlagsChanged), "flags")]
[JsonDerivedType(typeof(TrainerBeaten), "beaten")]
[JsonDerivedType(typeof(UseItemRequest), "useitem")]
[JsonDerivedType(typeof(GiveItemRequest), "giveitem")]
[JsonDerivedType(typeof(TakeItemRequest), "takeitem")]
[JsonDerivedType(typeof(DepositRequest), "deposit")]
[JsonDerivedType(typeof(WithdrawRequest), "withdraw")]
[JsonDerivedType(typeof(SwapPartyRequest), "swapparty")]
[JsonDerivedType(typeof(PartyOrdered), "partyorder")]
[JsonDerivedType(typeof(BoxUpdated), "boxupdate")]
[JsonDerivedType(typeof(BagUpdated), "bagupdate")]
[JsonDerivedType(typeof(PartyHealed), "healed")]
[JsonDerivedType(typeof(BlackedOut), "blackedout")]
[JsonDerivedType(typeof(TrainerSpotted), "spotted")]
[JsonDerivedType(typeof(ApproachEnded), "approachover")]
[JsonDerivedType(typeof(ItemFound), "itemfound")]
[JsonDerivedType(typeof(ObstacleShifted), "shifted")]
[JsonDerivedType(typeof(WentInside), "wentinside")]
[JsonDerivedType(typeof(TriggerFired), "triggered")]
[JsonDerivedType(typeof(ScriptGave), "scriptgave")]
[JsonDerivedType(typeof(ScriptFought), "scriptfought")]
[JsonDerivedType(typeof(NameMonRequest), "namemon")]
[JsonDerivedType(typeof(LearnMoveRequest), "learnmove")]
[JsonDerivedType(typeof(SurfRequest), "surf")]
[JsonDerivedType(typeof(SurfingChanged), "surfing")]
[JsonDerivedType(typeof(MoveOffered), "offered")]
[JsonDerivedType(typeof(HealRequest), "healplease")]
[JsonDerivedType(typeof(ConsoleCommand), "console")]
[JsonDerivedType(typeof(ConsoleReply), "consolesaid")]
[JsonDerivedType(typeof(ScenePlaced), "sceneplaced")]
[JsonDerivedType(typeof(SceneCast), "scenecast")]
[JsonDerivedType(typeof(BuyRequest), "buy")]
[JsonDerivedType(typeof(SellRequest), "sell")]
[JsonDerivedType(typeof(ShopOpened), "shop")]
[JsonDerivedType(typeof(FerryOpened), "ferry")]
[JsonDerivedType(typeof(SailRequest), "sail")]
[JsonDerivedType(typeof(ShopUpdated), "shopupdate")]
[JsonDerivedType(typeof(BuyCosmeticRequest), "buyclothes")]
[JsonDerivedType(typeof(ChatRequest), "chatask")]
[JsonDerivedType(typeof(ChatSaid), "chatsaid")]
[JsonDerivedType(typeof(DaycareUpdated), "daycare")]
[JsonDerivedType(typeof(DaycareRequest), "daycareask")]
[JsonDerivedType(typeof(GuildOpened), "guild")]
[JsonDerivedType(typeof(GuildRequest), "guildask")]
[JsonDerivedType(typeof(MarketOpened), "market")]
[JsonDerivedType(typeof(MarketRequest), "marketask")]
[JsonDerivedType(typeof(CosmeticsOwned), "owned")]
[JsonDerivedType(typeof(Welcome), "welcome")]
[JsonDerivedType(typeof(AuthFailed), "authfailed")]
[JsonDerivedType(typeof(MapChanged), "mapchanged")]
[JsonDerivedType(typeof(ObjectsPlaced), "objects")]
[JsonDerivedType(typeof(ObjectMoved), "objectmoved")]
[JsonDerivedType(typeof(PlayerAppeared), "appeared")]
[JsonDerivedType(typeof(AppearanceChanged), "looks")]
[JsonDerivedType(typeof(WearRequest), "wear")]
[JsonDerivedType(typeof(TradeRequest), "tradeask")]
[JsonDerivedType(typeof(DuelRequest), "duelask")]
[JsonDerivedType(typeof(DuelAsked), "duelasked")]
[JsonDerivedType(typeof(CompanyRequest), "companyask")]
[JsonDerivedType(typeof(CompanyAsked), "companyasked")]
[JsonDerivedType(typeof(CompanyLeaveRequest), "companyleave")]
[JsonDerivedType(typeof(TravellingWith), "travellingwith")]
[JsonDerivedType(typeof(TradeOffer), "tradeoffer")]
[JsonDerivedType(typeof(TradeConfirm), "tradeyes")]
[JsonDerivedType(typeof(TradeCancel), "tradestop")]
[JsonDerivedType(typeof(TradeUpdated), "tradestate")]
[JsonDerivedType(typeof(TradeAsked), "tradeasked")]
[JsonDerivedType(typeof(TradeEnded), "tradedone")]
[JsonDerivedType(typeof(PlayerMoved), "moved")]
[JsonDerivedType(typeof(PlayerHopped), "hopped")]
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
/// "Put me where they are."
/// <para>
/// A place can have more than one copy of itself, and two people who want to be together
/// can be in copies that cannot see each other — which, from inside, looks exactly like
/// the other person not being there. This is how an ordinary player asks to be moved
/// into somebody's copy. It was a console command first, and the console belongs to
/// operators, so the rule instancing owed was one only an operator could use.
/// </para>
/// <para>
/// By name rather than by id, because a name is what a player can see on another
/// player's head. The server refuses anything else — it never moves anybody off the map
/// they are standing on, whatever this asks for.
/// </para>
/// </summary>
public sealed record GoToRequest(string Name) : NetMessage;

/// <summary>
/// A square was stepped onto that runs a script.
/// <para>
/// The square rather than a name, because a trigger has no local id — it is not a person
/// and there is nothing standing there. The server checks the player is actually on it
/// and that the trigger's own condition still holds, which is the whole of its
/// involvement unless the square starts a fight.
/// </para>
/// <para>
/// <paramref name="TrainerId"/> is the fight the client's own run of the script arrived
/// at, and it is here because one square is not always one fight. The rival at the lab
/// door is three trainers, chosen by which starter was taken — a fact about the save,
/// which lives on the client's side of the cartridge. So the client names one and the
/// server checks the name is on the list that square is allowed to produce. Naming a
/// trainer this square cannot field is refused, which is what stops the message being a
/// way to ask for any fight in the game.
/// </para>
/// </summary>
public sealed record TriggerFired(int X, int Y, int? TrainerId = null) : NetMessage;

/// <summary>
/// A script just handed something over, and which one.
/// <para>
/// The same shape as a trigger naming a trainer, and for the same reason: which item, if
/// any, depends on what was said to a yes/no, and that is a fact about a save rather than
/// about a cartridge. So the client names it and the server checks the name against the
/// set the world file carries for that person — twenty-nine objects in this game hand
/// something over on a branch no fresh run ever walks, and both fossils in MT. MOON are
/// among them. Before this, "Obtained the DOME FOSSIL!" was on screen with nothing in
/// the bag.
/// </para>
/// </summary>
public sealed record ScriptGave(int LocalId, int ItemId) : NetMessage;

/// <summary>
/// A script started a fight with something that is not on any encounter table.
/// <para>
/// The same shape as <see cref="ScriptGave"/>, and the same reason: the client is the
/// only machine that can run the script, so it names the fight, and the server checks
/// the name against the set the world file carries for that person. Ten scripts in this
/// game set one up — the two sleepers, the three birds, and MEWTWO at level 70 — and a
/// client that could name its own would be a client that fights a level 5 MEWTWO.
/// </para>
/// </summary>
public sealed record ScriptFought(int LocalId, int Species, int Level) : NetMessage;

/// <summary>
/// A name for one of the party, given by the player on a screen this project had to
/// build itself.
/// <para>
/// The cartridge's keyboard is ARM code and cannot be read, so the script's call to it
/// returns and the client asks instead. The server keeps the answer because the server
/// keeps the party — and because a nickname a client held on its own would last until
/// the next sign-in.
/// </para>
/// </summary>
public sealed record NameMonRequest(int Slot, string Name) : NetMessage;

/// <summary>
/// Which of the four to drop for a move a level-up offered.
/// <para>
/// An answer rather than a request, which is the whole of its safety: the server put the
/// offer on a list when a level-up produced a fifth move, and nothing a client sends can
/// put anything on that list. Naming a move nobody was offered is refused.
/// </para>
/// <para>
/// <paramref name="Forget"/> outside the four means "keep what you have", which the games
/// allow and which is an answer rather than an error.
/// </para>
/// </summary>
public sealed record LearnMoveRequest(int MoveId, int Forget) : NetMessage;

/// <summary>
/// A line typed into the operator console.
/// <para>
/// Text, and nothing more. Every command is parsed and every effect decided on the
/// server, because a console the client acted on would be a cheat menu with extra steps
/// — and because the only account allowed to run one is named on the server's own
/// command line.
/// </para>
/// </summary>
/// <summary>
/// Yes, please heal them.
/// <para>
/// Asked for rather than done on arrival at the counter, because the counter asks. The
/// yes and the no live inside a standard routine — code this project cannot follow — so
/// the box is the client's and the answer has to travel. The server still decides: it
/// checks there is somebody who heals within reach before it does anything.
/// </para>
/// </summary>
public sealed record HealRequest : NetMessage;

public sealed record ConsoleCommand(string Text) : NetMessage;

/// <summary>What the console said back. One line, for the client to show.</summary>
public sealed record ConsoleReply(string Text) : NetMessage;

/// <summary>
/// Where a scene left somebody.
/// <para>
/// The client plays the scene, because the movements are on a cartridge the server has
/// never seen. That leaves the two sides disagreeing about where a person is standing the
/// moment the scene ends, and somebody has to say — so this says.
/// </para>
/// <para>
/// It is trusted narrowly and on purpose. The server accepts it only for somebody it is
/// already holding still for this player, only onto a square that is walkable and empty,
/// and only on the map they are both on. What that buys a determined client is the
/// ability to shuffle an NPC they are already standing in front of onto a walkable square
/// — which is worth strictly less than the alternative, which is every scene in the game
/// snapping its cast back the instant it ends.
/// </para>
/// </summary>
public sealed record ScenePlaced(int LocalId, int X, int Y, Direction Facing) : NetMessage;


/// <summary>
/// Who a scene is about, so they stand still for the duration.
/// <para>
/// Not <see cref="TalkRequest"/>, which is what this used to be and was wrong for the
/// reason talking is right: talking checks that the person is within reach, because a
/// conversation with somebody across the map is not a conversation. A scene is exactly
/// that — the professor is at the other end of the town when he starts walking over — so
/// every cast member out of arm's reach was refused, wandered off mid-scene, and had the
/// scene's final placement refused too because nobody was holding them.
/// </para>
/// <para>
/// Bounded the same way the rest of a scene is: only inside the window a trigger the server
/// agreed to fire opened, and only for people on the map the player is standing on.
/// </para>
/// </summary>
public sealed record SceneCast(IReadOnlyList<int> LocalIds) : NetMessage;

/// <summary>
/// What a script the player just ran did to their save.
/// <para>
/// Sent by the client because the client is the only machine that can run one: the
/// bytes are on a cartridge and the server has never had one. The server stores what
/// it is told without knowing what any of it means, which is the same arrangement as
/// the party — a save here is a list of numbers that only an image can resolve.
/// </para>
/// <para>
/// Not anti-cheat, and not pretending to be. A client that lied about a flag would be
/// lying about its own single-player progress, and the server keeps the two things
/// worth guarding — money and what is in the party — for itself.
/// </para>
/// </summary>
public sealed record ScriptRan(
    IReadOnlyList<int> Set,
    IReadOnlyList<int> Cleared,
    IReadOnlyList<SavedVariable> Written) : NetMessage;

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
    IReadOnlyList<SavedMon> Party) : NetMessage
{
    /// <summary>
    /// The save's script flags and variables, without which the client cannot tell
    /// which of somebody's lines is the one they are on.
    /// <para>
    /// Init properties rather than two more positional members, for the reason the save
    /// gives for the same choice: every existing construction is correct without them.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> Flags { get; init; } = [];

    /// <summary>
    /// What this account owns and what it has on.
    /// <para>
    /// Owned travels because a wardrobe screen with no list is not a wardrobe. It is still
    /// the server's — this is a copy for drawing, and every choice made from it comes back
    /// as a request the server is free to refuse.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> Cosmetics { get; init; } = [];

    public Appearance Looks { get; init; } = Appearance.Bare;

    public IReadOnlyList<SavedVariable> Variables { get; init; } = [];

    /// <summary>Who this character has already beaten, so their scripts run correctly.</summary>
    public IReadOnlyList<int> Beaten { get; init; } = [];

    /// <summary>Everything not in the party, and how much room the cartridge said there is.</summary>
    public IReadOnlyList<SavedMon> Box { get; init; } = [];

    public int BoxSize { get; init; }
}

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
/// Flags the server set or cleared without being asked.
/// <para>
/// The other direction of <see cref="ScriptRan"/>, and for a long time the direction
/// nothing ever went. This message existed, the client handled it, and the wire guardrail
/// carried a sample of it — and no line of the server ever built one. Every flag the
/// server changed on its own initiative was therefore a fact only one side of the split
/// knew, and the two sides went on answering the same question differently until somebody
/// noticed: the WARDEN kept asking for teeth that were already in the bag, because the
/// server had marked them picked up and the client had never been told.
/// </para>
/// <para>
/// Only setting, and no clearing. The console can turn a flag off, but it does that by
/// sending the whole save back — which is why the console half of this always worked and
/// the ball half never did. A field for clearing would be a field with no sender, and a
/// thing with no sender is what this message spent its life being.
/// </para>
/// </summary>
public sealed record FlagsChanged(IReadOnlyList<int> Flags) : NetMessage;

/// <summary>
/// A trainer this player has beaten.
/// <para>
/// Winning is decided on the server — the client has no party but its own and no say
/// in it — and until this arrives that trainer goes on reading their opening line,
/// because <c>trainerbattle</c> is its own conditional and the thing it asks is whether
/// this fight has already happened.
/// </para>
/// <para>
/// By id, not by flag. The word after the id in a <c>trainerbattle</c> command is not a
/// flag number, whatever it is: on a real image it is zero for every trainer on Route 8.
/// The id is this project's own and has been persisted since trainers existed.
/// </para>
/// </summary>
public sealed record TrainerBeaten(int TrainerId) : NetMessage;

/// <summary>
/// Drink something, on the party member in this slot.
/// <para>
/// An id and a slot and nothing else. How much it restores is the server's number for
/// the same reason it is in a battle — a request carrying the amount would let a client
/// drink a Potion for two hundred — and how much room there is to restore into depends
/// on maximum health, which is computed from base stats this end.
/// </para>
/// </summary>
public sealed record UseItemRequest(int ItemId, int Slot) : NetMessage;

/// <summary>
/// Hand this over, to the party member in this slot.
/// <para>
/// A separate request from using something rather than a mode of it, because the two
/// mean opposite things about the same item: a Potion used on somebody is drunk, and a
/// Potion handed to them is kept. A single verb would have to guess, and the games do
/// not — they ask.
/// </para>
/// </summary>
public sealed record GiveItemRequest(int ItemId, int Slot) : NetMessage;

/// <summary>
/// Take back whatever the party member in this slot is holding.
/// <para>
/// No item id. What they are holding is a thing the server knows and the client is only
/// shown, and a request that named the item would be a request a client could get wrong
/// — or lie about, and be handed something nobody was carrying.
/// </para>
/// </summary>
public sealed record TakeItemRequest(int Slot) : NetMessage;

/// <summary>Put the party member in this slot into the box.</summary>
public sealed record DepositRequest(int Slot) : NetMessage;

/// <summary>Take the one in this box slot back out.</summary>
public sealed record WithdrawRequest(int Slot) : NetMessage;

/// <summary>
/// Put these two party members in each other's places.
/// <para>
/// Two slots and nothing else. Which one ends up first is the only thing this changes
/// and the only thing it needs to say — the party is the party, and a request carrying
/// a whole new order would be a client handing the server a party.
/// </para>
/// </summary>
public sealed record SwapPartyRequest(int A, int B) : NetMessage;

/// <summary>The party after it was rearranged, with a line about what happened.</summary>
public sealed record PartyOrdered(IReadOnlyList<SavedMon> Party, string Message) : NetMessage;

/// <summary>
/// The party and the box after one moved between them.
/// <para>
/// Both lists, for the same reason a bag update carries both the bag and the party:
/// every one of these operations changes two things at once, and a client holding one
/// of them stale shows a creature in two places or in neither.
/// </para>
/// <para>
/// The size comes with it because it is the cartridge's number and the client has no
/// other way to know it. A client that assumed thirty would be remembering.
/// </para>
/// </summary>
public sealed record BoxUpdated(
    IReadOnlyList<SavedMon> Party,
    IReadOnlyList<SavedMon> Box,
    int BoxSize,
    string Message) : NetMessage;

/// <summary>
/// One person in a guild, as a screen needs them.
/// <para>
/// The roster half comes off the disk and the "where are they" half comes off the world,
/// and they are one record here because a screen shows them on one line. Keeping them apart
/// is right in the store, where one is durable and the other is only true for an instant;
/// keeping them apart on the wire would just mean the client joining two lists by name.
/// </para>
/// </summary>
public sealed record GuildFace(string Name, bool IsLeader, string Where);

/// <summary>
/// A guild, as one whole picture — or the offers to join one, when there is none.
/// <para>
/// One message for both states, because they are the same screen answering the same
/// question from either side of having a guild. A client with no guild and three invitations
/// has something to show; a second message kind for it would be a second thing to keep in
/// step.
/// </para>
/// </summary>
public sealed record GuildOpened(
    string Name,
    IReadOnlyList<GuildFace> Members,
    IReadOnlyList<string> Invitations,
    bool IsLeader,
    string Message) : NetMessage
{
    /// <summary>True when this player is in one at all.</summary>
    public bool Exists => Name.Length > 0;
}

/// <summary>The five things anybody does to a guild from a screen.</summary>
public enum GuildAsk
{
    /// <summary>Just show me. Sent when the screen opens.</summary>
    Look,
    Found,
    Invite,
    Join,
    Leave,
    Kick,
}

/// <summary>
/// What a screen asks about a guild.
/// <para>
/// One message with a kind and one name, for the reason the market's has one: they differ in
/// what the name means and in nothing else, and five near-identical records would each need
/// a handler saying the same three things.
/// </para>
/// </summary>
public sealed record GuildRequest(GuildAsk Asking, string Name = "") : NetMessage;

/// <summary>
/// The market, as one whole picture.
/// <para>
/// Everything a screen needs in one message, rather than a board here and a purse there:
/// every act at a market changes at least two of these at once — buying moves a creature
/// <em>and</em> money <em>and</em> takes a row off the board — and a client holding any of
/// them stale is showing somebody a market that no longer exists.
/// </para>
/// <para>
/// It carries the seller's own box and bag as well, which looks like more than a market
/// needs until you ask what a screen is for. The thing you do at a market is decide, and
/// deciding what to sell means looking at what you have next to what everybody else is
/// asking for it. That comparison is the whole screen.
/// </para>
/// </summary>
public sealed record MarketOpened(
    IReadOnlyList<Listing> Board,
    IReadOnlyList<Listing> Mine,
    IReadOnlyList<SavedMon> Box,
    IReadOnlyList<BagEntry> Bag,
    int Money,
    string Message) : NetMessage
{
    /// <summary>What is waiting to be collected, already with the cut taken off.</summary>
    public int Owed { get; init; }

    /// <summary>
    /// What the market keeps, as a percentage, so the screen can say so rather than let
    /// somebody discover it by counting their money afterwards.
    /// </summary>
    public int Cut { get; init; }
}

/// <summary>
/// The six things anybody does at a market.
/// <para>
/// Named rather than numbered because the wire form is a string either way and a reader
/// of a packet dump should not have to look up what four meant.
/// </para>
/// </summary>
public enum MarketAsk
{
    /// <summary>Just show me. Sent when the screen opens and after nothing in particular.</summary>
    Look,
    Buy,
    Cancel,
    Collect,

    /// <summary>One creature out of the box, at a price.</summary>
    SellOne,

    /// <summary>A number of one item out of the bag, at a price for the lot.</summary>
    SellSome,
}

/// <summary>
/// What a screen asks the market to do.
/// <para>
/// One message with a kind rather than six, for the reason the daycare has one: they
/// differ in which fields matter and in nothing else, and six near-identical records would
/// each need their own handler saying the same three things.
/// </para>
/// <para>
/// Everything on it is a number the server checks. A client may say "sell box slot two for
/// one", and what comes back is either a market with that on it or a market with a sentence
/// explaining why not — the screen is never the thing that refused.
/// </para>
/// </summary>
public sealed record MarketRequest(MarketAsk Asking) : NetMessage
{
    /// <summary>Which listing, for buying and cancelling.</summary>
    public long Listing { get; init; }

    /// <summary>Which box slot, for selling a creature.</summary>
    public int Slot { get; init; }

    /// <summary>Which item, for selling a pile.</summary>
    public int Item { get; init; }

    /// <summary>How many of it.</summary>
    public int Count { get; init; }

    /// <summary>What is being asked for it, for the lot rather than for each.</summary>
    public int Price { get; init; }
}

/// <summary>
/// What is on the daycare's shelf, and how far off an egg is.
/// <para>
/// One message for opening the place and for every change to it, the way
/// <see cref="BoxUpdated"/> is. Two would be two ways of saying the same thing, and the
/// second one is always the one that drifts.
/// </para>
/// <para>
/// The party comes with it for the reason it comes with the box: leaving somebody there
/// changes both lists at once, and a client holding one of them stale shows a creature in
/// two places or in neither.
/// </para>
/// </summary>
public sealed record DaycareUpdated(
    IReadOnlyList<SavedMon> Party,
    IReadOnlyList<SavedMon> Minded,
    int StepsToEgg,
    string Message) : NetMessage
{
    /// <summary>
    /// How many one of these holds. The client draws that many places on the shelf, and
    /// two empty ones say what one empty one and a guess cannot.
    /// </summary>
    public int Holds { get; init; } = 2;
}

/// <summary>
/// Leaving one, or taking one back.
/// <para>
/// One message for both, with a flag, rather than two nearly identical ones. Which
/// direction it goes decides which list the slot is an index into, and nothing else — so
/// two messages would differ only in their names and would each need their own handler
/// saying the same things.
/// </para>
/// <para>
/// A slot and a direction is the whole of what a client gets to say. Whether there is
/// anywhere to leave anybody, whether that one is the last that can fight, and whether the
/// pair will produce anything are all the server's, checked against the world file it
/// loaded rather than against anything that arrived over a socket.
/// </para>
/// </summary>
public sealed record DaycareRequest(int Slot, bool Leaving) : NetMessage;

/// <summary>
/// The bag and the party after something was used out of a fight.
/// <para>
/// Both, because using a potion changes both and a client holding one of them stale
/// shows a bag that has spent an item on somebody who is still hurt.
/// </para>
/// </summary>
public sealed record BagUpdated(
    IReadOnlyList<BagEntry> Bag,
    IReadOnlyList<SavedMon> Party,
    string Message) : NetMessage
{
    /// <summary>
    /// What just became what, when something did. Zero the rest of the time.
    /// <para>
    /// Two species numbers rather than a sentence, for the reason every message in this
    /// project carries numbers: the server has never seen a name. The client already
    /// says this line inside a battle and now says the same one out of it, off the same
    /// two numbers, which is how both come to be phrased identically without either
    /// half knowing the other exists.
    /// </para>
    /// </summary>
    public int EvolvedFrom { get; init; }

    public int EvolvedInto { get; init; }
}

/// <summary>
/// The party, put back on its feet at a counter.
/// <para>
/// <paramref name="Needed"/> is here so the client can say something true. A centre
/// that reports a miracle to somebody who walked in healthy is the kind of small lie
/// that makes a player stop believing the rest of it.
/// </para>
/// </summary>
public sealed record PartyHealed(IReadOnlyList<SavedMon> Party, bool Needed) : NetMessage;

/// <summary>
/// Woken up at the last centre, lighter, after a party was wiped out.
/// <para>
/// Sent alongside the map change rather than instead of it, because the client needs
/// both: the world moved, and so did the money. Separating them would leave a client
/// that missed one showing a healthy party in the wrong town.
/// </para>
/// </summary>
/// <summary>
/// Somebody has noticed the player and is on their way over.
/// <para>
/// Sent when the sight line is crossed rather than when the fight begins, because
/// between those two things is a walk, and the walk is the part everybody remembers —
/// it is why you learn to hug the far wall of a route rather than stroll down the
/// middle of it. The player stands still for it, which the server enforces and this
/// message is what lets the client stop asking.
/// </para>
/// </summary>
public sealed record TrainerSpotted(int LocalId) : NetMessage;

/// <summary>
/// Nobody is walking over any more, and no fight came of it.
/// <para>
/// The end of a walk is almost always a battle, and a battle says so loudly. This is
/// for the times it is not: a trainer with no party the server can field, a player
/// whose own party is in no state to fight, a walk abandoned because its target went
/// through a door. Without it the client would keep standing still and waiting for a
/// fight that is not coming, which is the same class of bug as a conversation nobody
/// ever ends.
/// </para>
/// </summary>
public sealed record ApproachEnded : NetMessage;

/// <summary>
/// Something picked up off the ground.
/// <para>
/// The id rather than the name, because the server has never seen a cartridge and the
/// client has one open. Same arrangement as the party: numbers here, names there.
/// </para>
/// </summary>
public sealed record ItemFound(int ItemId, int Count, IReadOnlyList<BagEntry> Bag) : NetMessage;

/// <summary>
/// Somebody a scene was walking has gone in through a door.
/// <para>
/// The professor does not stand in his own doorway at the end of the opening; he goes
/// inside, and so does everybody else a scene walks onto a door. The cartridge says as
/// much in the block data — a door's square is solid, and this game opens it so people
/// can walk through rather than so they can stand there.
/// </para>
/// <para>
/// Sent to one player, like a felled tree and for the same reason: it is that player's
/// scene that put them there, and it lasts until they leave the map.
/// </para>
/// </summary>
public sealed record WentInside(int LocalId) : NetMessage;

/// <summary>
/// Something in the way has been moved out of it — a tree cut, a boulder pushed, rubble
/// broken.
/// <para>
/// Sent to one player and to nobody else, which is the whole design decision here. A
/// tree is scenery in a single-player game and a shared object in this one, and felling
/// it for everybody on the map would let one person quietly open every route in the
/// world for strangers. It stays down until they leave the map, which is also what the
/// games do.
/// </para>
/// </summary>
public sealed record ObstacleShifted(int LocalId, int MoveId, int Slot) : NetMessage;

public sealed record BlackedOut(
    string MapId,
    int X,
    int Y,
    int Money,
    IReadOnlyList<SavedMon> Party) : NetMessage;

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
/// <summary>
/// Somebody standing on a map, as the server sees them.
/// <para>
/// <c>Heals</c> is the odd one out and it is here for a reason. Every other thing the
/// client knows about a person it reads off the cartridge itself — but who heals is not
/// written on any person; it is worked out by noticing that twenty scripts across twenty
/// maps all hand off to one address, and that scan wants the whole world at once. The
/// export does it, so the export is what says so.
/// </para>
/// <para>
/// The standing rule again, in the direction that is easy to miss: the server refuses to
/// heal where nobody heals, and this is the client's half of that same fact — without it
/// the counter asks nothing and the refusal never comes up, because the question is never
/// put.
/// </para>
/// </summary>
public sealed record ObjectView(int LocalId, int GraphicsId, int X, int Y, Direction Facing, bool Heals = false)
{
    /// <summary>
    /// True when two can be left with this one.
    /// <para>
    /// The client is told for the same reason it is told who heals: a rule enforced on one
    /// side of the split needs its counterpart on the other, or the only way to find out
    /// that somebody minds creatures is to walk up and be refused.
    /// </para>
    /// </summary>
    public bool MindsCreatures { get; init; }
}

/// <summary>Everyone standing on the map a player has just arrived on.</summary>
public sealed record ObjectsPlaced(IReadOnlyList<ObjectView> Objects) : NetMessage;

/// <summary>One of them turned or took a step.</summary>
public sealed record ObjectMoved(int LocalId, int X, int Y, Direction Facing) : NetMessage;

/// <summary>Another player is now visible — sent on join, and for everyone already present.</summary>
public sealed record PlayerAppeared(int PlayerId, string Name, int X, int Y, Direction Facing) : NetMessage
{
    /// <summary>
    /// What they are wearing, which is the whole of what one player is told about another
    /// beyond their name and their square.
    /// <para>
    /// An init property rather than a positional one, for the reason every other addition
    /// to this file gives: every existing construction is correct without it. It defaults
    /// to bare, so a client that has never heard of cosmetics draws what it always drew.
    /// </para>
    /// </summary>
    public Appearance Looks { get; init; } = Appearance.Bare;
}

/// <summary>
/// Asking somebody to trade, or agreeing to trade with somebody who asked you.
/// <para>
/// One message for both, because they are the same act: two requests pointing at each other
/// is the whole handshake, and a separate "yes" would be a second way to say a thing that is
/// already unambiguous.
/// </para>
/// </summary>
public sealed record TradeRequest(int WithPlayerId) : NetMessage;

/// <summary>
/// Asking somebody for a fight, or agreeing to one.
/// <para>
/// The same shape as <see cref="TradeRequest"/> and the same handshake: two requests
/// pointing at each other and nothing else. What comes back is a battle, in the same
/// messages a battle has always used, which is why this is the only new thing a client
/// needs to know to fight another player.
/// </para>
/// </summary>
public sealed record DuelRequest(int WithPlayerId) : NetMessage;

/// <summary>Somebody has challenged you, and asking back is how you accept.</summary>
public sealed record DuelAsked(int FromPlayerId, string FromName) : NetMessage;

/// <summary>
/// Asks somebody to travel together, and accepts when they have already asked.
/// <para>
/// The same handshake as a trade and a duel — two requests pointing at each other — because
/// a player should only have to learn it once. What it buys is that the two of you land in
/// the same copy of everywhere you go, which walking through the same door only does by
/// accident and only until one of you takes a different route.
/// </para>
/// </summary>
public sealed record CompanyRequest(int WithPlayerId) : NetMessage;

/// <summary>Somebody has asked you to travel together; asking back is how you accept.</summary>
public sealed record CompanyAsked(int FromPlayerId, string FromName) : NetMessage;

/// <summary>Stop travelling with whoever you are travelling with.</summary>
public sealed record CompanyLeaveRequest : NetMessage;

/// <summary>
/// Who you are travelling with now, sent to everybody it changed for.
/// <para>
/// The whole party each time rather than "so-and-so joined", because a client that had to
/// build the list from arrivals and departures would be a second copy of the server's list,
/// and the two would disagree the first time a message was missed.
/// </para>
/// <para>
/// An empty list means you are travelling alone, which is how a party ending is said. There
/// is no separate message for it — the same reason a player walking out of sight sends the
/// message a disconnect sends.
/// </para>
/// </summary>
public sealed record TravellingWith(IReadOnlyList<int> PlayerIds, IReadOnlyList<string> Names) : NetMessage;

/// <summary>Putting a party slot up, or −1 to take it back down.</summary>
public sealed record TradeOffer(int Slot) : NetMessage;

/// <summary>Saying yes to what is on the table, or taking that back.</summary>
public sealed record TradeConfirm(bool Ready) : NetMessage;

/// <summary>Walking away.</summary>
public sealed record TradeCancel : NetMessage;

/// <summary>
/// Everything about a trade in progress, in one message.
/// <para>
/// One state message rather than half a dozen events, because a client that assembled the
/// state from events would be a client that can be one event behind — and being one event
/// behind about what is on the table is exactly the thing a trade cannot afford.
/// </para>
/// </summary>
public sealed record TradeUpdated(
    int WithPlayerId,
    string WithName,
    SavedMon? Yours,
    SavedMon? Theirs,
    bool YouAgreed,
    bool TheyAgreed) : NetMessage;

/// <summary>
/// Somebody has asked to trade. Not the same as a trade being open — nothing is on the
/// table until it is answered.
/// </summary>
public sealed record TradeAsked(int FromPlayerId, string FromName) : NetMessage;

/// <summary>A trade is over, done or not, with whatever the party is now.</summary>
public sealed record TradeEnded(string Reason, IReadOnlyList<SavedMon> Party) : NetMessage;

/// <summary>Somebody put something on or took it off.</summary>
public sealed record AppearanceChanged(int PlayerId, Appearance Looks) : NetMessage;

/// <summary>
/// A player asking to wear something, or to take a slot off with id zero.
/// <para>
/// Asking, not telling. What an account owns is the server's to know — a client that
/// decided what it was wearing would be a client that wears whatever it has been edited to
/// wear, and the whole point of a thing being sold is that not everybody has it.
/// </para>
/// </summary>
public sealed record WearRequest(int CosmeticId, CosmeticSlot Slot) : NetMessage;

/// <summary>A player stepped. Sent to everyone, including the player who moved.</summary>
public sealed record PlayerMoved(int PlayerId, int X, int Y, Direction Facing) : NetMessage;

/// <summary>
/// A player went over a ledge: two squares, in one movement.
/// <para>
/// Its own message rather than a move with a flag on it, because everyone watching has
/// to draw it differently — a figure that slid two squares in the time a step takes
/// reads as a glitch, and the arc is the only thing that says what happened.
/// </para>
/// </summary>
public sealed record PlayerHopped(int PlayerId, int X, int Y, Direction Facing) : NetMessage;

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
    IReadOnlyList<int> Moves)
{
    /// <summary>
    /// What is left of each move, in the same order as <see cref="Moves"/>.
    /// <para>
    /// So a client can grey out a move with nothing left in it rather than offering one
    /// the server will refuse. Empty for the other side, whose PP is not the player's
    /// business — the games do not show it and neither does this.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> Pp { get; init; } = [];
}

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
    int? TrainerId = null,

    /// <summary>Which party slot is out, so a client can offer the other five.</summary>
    int Slot = 0) : NetMessage
{
    /// <summary>
    /// The player on the other side, when the other side is a player.
    /// <para>
    /// A fight in this game has always been either something in the grass or a trainer
    /// with a number, and the client tells them apart by whether there is a number. A
    /// duel is neither: there is nobody to look up, and without a name for the other
    /// chair the first line of the first duel this game ever fought was "A wild SQUIRTLE
    /// appeared!"
    /// </para>
    /// </summary>
    public string? Against { get; init; }
}

/// <summary>
/// A move somebody has been offered and cannot fit, outside a battle.
/// <para>
/// The battle screen learns about these from the events of the turn that produced them.
/// A machine produces one with no turn around it, so it is said out loud — and the
/// answer goes back the same way either one does, as a <see cref="LearnMoveRequest"/>
/// the server checks against the list it is holding.
/// </para>
/// </summary>
public sealed record MoveOffered(int Slot, int MoveId) : NetMessage;

/// <summary>
/// A player asking to get onto the water in front of them.
/// <para>
/// A request, like everything else a client sends. What it does not carry is who in the
/// party knows how — the server holds the party and can see for itself, and a client
/// that named the swimmer could name one that cannot swim.
/// </para>
/// </summary>
public sealed record SurfRequest : NetMessage;

/// <summary>
/// Whether this player is on the water now, and where they are standing.
/// <para>
/// The square travels with it because getting on the water is also a step, and a client
/// that changed its own grid and then waited to be told where it was standing would draw
/// the player on the shore for as long as the round trip took.
/// </para>
/// </summary>
public sealed record SurfingChanged(bool Surfing, int X, int Y) : NetMessage;

/// <summary>
/// One side has sent out somebody new.
/// <para>
/// Its own message rather than a field on <see cref="BattleUpdate"/>, because it
/// happens between turns rather than during one: whoever fainted did so as part of the
/// turn just reported, and this is what comes next.
/// </para>
/// </summary>
public sealed record BattlerSentOut(Side Side, BattlerView Battler, int Slot = 0) : NetMessage;

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
    IReadOnlyList<BagEntry> Medicine,

    /// <summary>
    /// The party as it stands, because a battle is where it changes.
    /// <para>
    /// Sent every turn rather than at the end, now that a player can choose who comes
    /// out: a list built from the party as it was at the start of the fight would offer
    /// somebody who fainted two turns ago.
    /// </para>
    /// </summary>
    IReadOnlyList<SavedMon>? Party = null) : NetMessage
{
    /// <summary>
    /// Which move the next turn is already spoken for, or nothing when there is a choice.
    /// <para>
    /// Halfway through THRASH, or in the air with FLY, there is no decision to make: the
    /// engine takes the move it is holding whatever arrives. The server was already doing
    /// that and the client was still drawing four lit options and taking a keypress, which
    /// is the shape of every client/server disagreement this project has had — one side
    /// enforcing a rule and the other not knowing there is one.
    /// </para>
    /// </summary>
    public int? NoChoiceBut { get; init; }

    /// <summary>
    /// What is left of each of the player's own moves, after this turn.
    /// <para>
    /// Sent every turn for the same reason the party is: it changes every turn, and a
    /// number drawn from the move's record instead is right once and wrong from then on.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> Pp { get; init; } = [];

    /// <summary>
    /// The slot the player may not use, or nothing.
    /// <para>
    /// The counterpart of <see cref="NoChoiceBut"/> and here for the same reason: a rule
    /// this side enforces and the other side does not know about is a button that is
    /// offered and refused, which is the shape of every client/server disagreement this
    /// project has had.
    /// </para>
    /// </summary>
    public int? Disabled { get; init; }
}

/// <summary>
/// The battle is over. Carries the party back because it may have just grown.
/// </summary>
public sealed record BattleFinished(
    Side? Winner,
    bool Caught,
    int Money,
    int Prize,
    IReadOnlyList<BagEntry> Balls,
    IReadOnlyList<SavedMon> Party) : NetMessage
{
    /// <summary>
    /// True when what was caught went to the box rather than the party.
    /// <para>
    /// Carried rather than worked out by the client from the party not having grown.
    /// That inference is available and wrong the moment anything else can change a
    /// party mid-fight, and it reads as "nothing happened", which is precisely the
    /// silence this field exists to end.
    /// </para>
    /// </summary>
    public bool ToTheBox { get; init; }

    /// <summary>
    /// The box, for the same reason the party comes back: a fight can change it.
    /// <para>
    /// Only a catch can, and only when the party is full, which is rare enough that the
    /// first version left it out — and then the box screen opened on a box that was
    /// accurate at login and did not contain the creature the player had just been told
    /// went into it.
    /// </para>
    /// </summary>
    public IReadOnlyList<SavedMon> Box { get; init; } = [];
}

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
    IReadOnlyList<BagEntry> Bag) : NetMessage
{
    /// <summary>
    /// What this counter will sell you to wear, which is everything the wardrobe holds
    /// that this account does not already own.
    /// <para>
    /// The same <see cref="ShopEntry"/> shape as the items, and that is a shortcut worth
    /// naming rather than hiding: the id in one is an item and in the other a cosmetic,
    /// and those are different numbering. Anything that looks one of them up in the
    /// other's table gets a wrong name rather than an error, which is the kind of bug
    /// that survives a review.
    /// </para>
    /// <para>
    /// An init property, so every existing construction of this message stays correct and
    /// a counter with nothing to wear simply sends none.
    /// </para>
    /// </summary>
    public IReadOnlyList<ShopEntry> Clothes { get; init; } = [];
}

/// <summary>
/// Something a player is saying.
/// <para>
/// <paramref name="To"/> is nobody for the ordinary case, which is everybody in the copy of
/// the place you are standing in — and the copy, rather than the map, is the point. Two
/// people in different copies of one town cannot see each other, so a chat scoped to the
/// map would put words in the mouths of people who are not there.
/// </para>
/// <para>
/// A name rather than an id for a whisper, because a name is what a player can read off
/// somebody's head. The server refuses anything it cannot find.
/// </para>
/// </summary>
public sealed record ChatRequest(string Text, string? To = null) : NetMessage;

/// <summary>
/// Something that was said, and who said it.
/// <para>
/// The name travels with it rather than being looked up from the id. A client that had to
/// resolve a name would have nothing to show for somebody who has just walked out of
/// sight, which is exactly when the last thing they said still matters.
/// </para>
/// </summary>
public sealed record ChatSaid(int PlayerId, string Name, string Text) : NetMessage
{
    /// <summary>True when this was said to one person rather than to a room.</summary>
    public bool Private { get; init; }

    /// <summary>
    /// True when this is the copy sent back to whoever said it.
    /// <para>
    /// A whisper reaches two people and reads differently to each: one of them needs to see
    /// who it went to, and the other who it came from. Rather than two message shapes, the
    /// sender's copy is flagged and the client picks the wording.
    /// </para>
    /// </summary>
    public bool Mine { get; init; }
}

/// <summary>
/// Buy something to wear from the counter that is open.
/// <para>
/// The id alone. What it costs, whether this counter sells it, and whether it is already
/// owned are all the server's, checked against the wardrobe rather than against anything
/// that arrived over a socket.
/// </para>
/// </summary>
public sealed record BuyCosmeticRequest(int CosmeticId) : NetMessage;

/// <summary>
/// What this account owns, after it changed.
/// <para>
/// Its own message rather than another field on <see cref="ShopUpdated"/>, because what an
/// account owns changes in more places than a shop: an operator's <c>/grant</c> does it
/// too, and that has been silently broken for as long as it has existed. The owned list
/// only ever reached a client in <see cref="Welcome"/>, so a granted hat appeared the next
/// time somebody logged in and not before — and nobody noticed, because until there was a
/// shop the wardrobe was only ever opened after a login.
/// </para>
/// <para>
/// One message that says "this is what you own now" fixes both, and is the shape that stays
/// right when the third thing that grants a cosmetic arrives.
/// </para>
/// </summary>
public sealed record CosmeticsOwned(IReadOnlyList<int> Owned) : NetMessage;

/// <summary>
/// What the money and the bag are after something was bought or sold.
/// <para>
/// Sent instead of a yes or no. A refusal and a purchase differ only in what these two
/// numbers become, and a client that had to work out which happened would eventually
/// work it out wrongly.
/// </para>
/// </summary>
public sealed record ShopUpdated(int Money, IReadOnlyList<BagEntry> Bag, string Message) : NetMessage;

/// <summary>One place the boat calls at, as the client is told about it.</summary>
public sealed record FerryPort(int Number, string MapId, string Name);

/// <summary>
/// The sailor was asked, and this is where he can take you.
/// <para>
/// Built like a shop and for the same reason: the list is the server's, the choosing is
/// the client's, and the crossing is checked again on this side when it comes back.
/// </para>
/// <para>
/// Which places appear is not yet the cartridge's question. The real ferry asks for a
/// pass — two flags and two items, all of them in the VERMILION script — and until that
/// is derived properly this offers everywhere the boat calls, and says so out loud rather
/// than pretending to a gate it has not read.
/// </para>
/// </summary>
public sealed record FerryOpened(int From, IReadOnlyList<FerryPort> Ports) : NetMessage;

/// <summary>Take me there.</summary>
public sealed record SailRequest(int Number) : NetMessage;

/// <summary>The request could not be honoured at all.</summary>
public sealed record Rejected(string Reason) : NetMessage;

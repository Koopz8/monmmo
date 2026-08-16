using System.Reflection;
using System.Buffers.Binary;
using PokeMmo.Core.Battle;
using PokeMmo.Core.Cosmetics;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A stream that hands back at most <paramref name="chunkSize"/> bytes per read,
/// which is how a real socket behaves under load and how framing bugs surface.
/// </summary>
internal sealed class DripStream(byte[] data, int chunkSize) : Stream
{
    private int _position;

    public override int Read(byte[] buffer, int offset, int count)
    {
        int available = Math.Min(Math.Min(chunkSize, count), data.Length - _position);
        if (available <= 0) return 0;

        Array.Copy(data, _position, buffer, offset, available);
        _position += available;
        return available;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => data.Length;
    public override long Position { get => _position; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

public class MessageChannelTests
{
    private static async Task<byte[]> EncodeAsync(params NetMessage[] messages)
    {
        using var buffer = new MemoryStream();
        var channel = new MessageChannel(buffer);

        foreach (NetMessage message in messages) await channel.SendAsync(message);

        return buffer.ToArray();
    }

    /// <summary>One of every kind of message, filled in enough to be worth comparing.</summary>
    private static NetMessage[] SampleMessages() =>
        [
            new RegisterRequest("Mason", "a-good-password"),
            new BuyCosmeticRequest(103),
            new ChatRequest("hello"),
            new ChatRequest("psst", "Koop"),
            new ChatSaid(1, "Mason", "hello"),
            new ChatSaid(1, "Koop", "psst") { Private = true, Mine = true },
            new CosmeticsOwned([101, 103]),
            new DaycareRequest(1, true),
            new DaycareUpdated(
                [new SavedMon(1, 5, null, 19, StatusCondition.None, Nature.Hardy, [33])],
                [new SavedMon(16, 20, null, 40, StatusCondition.None, Nature.Bold, [33])],
                3_940,
                "We can raise them for you."),
            new GuildRequest(GuildAsk.Invite, "Ash"),

            // Both states of the one message: a guild with people in it, and the offers
            // somebody with no guild is looking at.
            new GuildOpened(
                "Team Rocket",
                [new GuildFace("Mason", true, "PALLET TOWN"), new GuildFace("Ash", false, "")],
                [],
                IsLeader: true,
                "Ash has been asked"),

            new GuildOpened("", [], ["Team Rocket", "Team Aqua"], IsLeader: false, ""),

            new MarketRequest(MarketAsk.SellSome) { Item = 13, Count = 5, Price = 900 },

            // Both kinds of listing, because a listing that is a pile of items and one that
            // is a creature go down the same wire in the same field, and a screen that
            // could only read one of them back would be a screen with half a market on it.
            new MarketOpened(
                [
                    new Listing(4, "Koop", 150, 44, 2_500)
                    {
                        Ivs = [31, 30, 29, 28, 27, 26],
                        Sex = Gender.Female,
                    },
                    new Listing(5, "Koop", 0, 0, 900) { Item = 13, Count = 5 },
                ],
                [new Listing(6, "Mason", 0, 0, 400) { Item = 14, Count = 2, Sold = true }],
                [new SavedMon(7, 12, null, 30, StatusCondition.None, Nature.Bold, [33])],
                [new BagEntry(13, 7)],
                5_000,
                "bought species 150 for 2500")
            {
                Owed = 380,
                Cut = 5,
            },

            new LoginRequest("Mason", "a-good-password"),
            new ScriptGave(1, 358),
            new LearnMoveRequest(33, 2),
            new SurfRequest(),
            new GoToRequest("Mason"),
            new MoveOffered(1, 57),
            new SurfingChanged(true, 9, 12),
            new PlayerHopped(1, 9, 12, Direction.Down),
            new ScriptFought(5, 143, 30),
            new BattleTurn(new BattleAction.UseMove(2)),
            new BattleTurn(new BattleAction.ThrowBall(4) { Kind = BallKind.Great }),
            new BattleStarted(
                new BattlerView(1, 5, "Bulby", 19, 20, StatusCondition.None, [33]),
                new BattlerView(16, 3, null, 11, 11, StatusCondition.None, [33, 45]),
                [new BagEntry(4, 12)],
                [new BagEntry(13, 2)],
                TrainerId: 214),
            new BattleUpdate(
                [
                    new BattleEvent.MoveUsed(Side.Player, 33),
                    new BattleEvent.ScreenRose(Side.Player, Physical: true),
                    new BattleEvent.Seeded(Side.Opponent),
                    new BattleEvent.Sapped(Side.Opponent, 6, 12),
                    new BattleEvent.WallsBroke(Side.Opponent),
                    new BattleEvent.KnockedOff(Side.Opponent, 200),
                    new BattleEvent.ShookFree(Side.Player),
                    new BattleEvent.Identified(Side.Opponent),
                    new BattleEvent.HurtBySleep(Side.Opponent, 5, 7),
                    new BattleEvent.Drowsy(Side.Opponent),
                    new BattleEvent.TookRoot(Side.Player),
                    new BattleEvent.PerishCount(Side.Player, 3),
                    new BattleEvent.Taunted(Side.Opponent),
                    new BattleEvent.Tormented(Side.Opponent),
                    new BattleEvent.BracedItself(Side.Player),
                    new BattleEvent.Endured(Side.Player),
                    new BattleEvent.Bonded(Side.Player),
                    new BattleEvent.TookThemWith(Side.Opponent),
                    new BattleEvent.HealthShared(Side.Player, 14),
                    new BattleEvent.Fainted(Side.Opponent),
                ],
                19, 0, [new BagEntry(4, 12)], [new BagEntry(13, 2)]),
            new BattleFinished(
                Side.Player, true, 5400, 400, [new BagEntry(4, 11)],
                [new SavedMon(16, 3, null, 1, StatusCondition.None, Nature.Bold, [33])]),
            new MoveRequest(Direction.Left),
            new BattleTurn(new BattleAction.UseItem(13) { Restores = 20 }),
            new BattlerSentOut(Side.Opponent, new BattlerView(4, 9, null, 22, 26, StatusCondition.None, [10])),
            new TalkRequest(4),
            new TalkFinished(),
            new ScriptRan([0x2A5], [0x828], [new SavedVariable(0x4001, 3)]),
            new FlagsChanged([0x4F1]),
            new TrainerBeaten(41),
            new UseItemRequest(13, 0),
            new GiveItemRequest(207, 2),
            new TakeItemRequest(2),
            new DepositRequest(1),
            new WithdrawRequest(0),
            new SwapPartyRequest(0, 2),
            new PartyOrdered([], "Leading now."),
            new BoxUpdated([], [], 30, "Into the box."),
            new BagUpdated([new BagEntry(13, 2)], [], "Restored 20 health."),
            new PartyHealed([], true),
            new BlackedOut("3.0", 7, 4, 1500, []),
            new TrainerSpotted(3),
            new ApproachEnded(),
            new ItemFound(13, 2, [new BagEntry(13, 2)]),
            new WentInside(3),
            new ObstacleShifted(4, 15, 1),
            new TriggerFired(9, 12),
            new NameMonRequest(0, "SPROUT"),
            new HealRequest(),
            new ConsoleCommand("/tp 3.1 25 20"),
            new ConsoleReply("moved to VIRIDIAN CITY (25, 20)"),
            new ScenePlaced(2, 5, 6, Direction.Left),
            new SceneCast([3, 4]),
            new BuyRequest(4, 5),
            new SellRequest(13, 2),
            new ShopOpened([new ShopEntry(4, 200)], 5000, [new BagEntry(4, 3)]),
            new FerryOpened(0, [new FerryPort(1, "32.4", "ONE ISLAND")]),
            new DuelRequest(2),
            new DuelAsked(2, "Koop"),
            new SailRequest(1),
            new ShopUpdated(4800, [new BagEntry(4, 4)], "Bought 1."),
            new MapChanged("3.1", 4, 9, Direction.Down),
            new ObjectsPlaced([new ObjectView(1, 5, 3, 3, Direction.Left)]),
            new ObjectMoved(1, 3, 4, Direction.Down),
            new Welcome(
                7, "3.0", 12, 5, Direction.Up, 5000, [new BagEntry(4, 20)],
                [new SavedMon(1, 5, null, 19, StatusCondition.None, Nature.Hardy, [33])]),
            new PlayerAppeared(9, "Someone", 1, 2, Direction.Right)
            {
                Looks = Appearance.Bare.Wearing(Wardrobe.At(301)!),
            },
            new AppearanceChanged(9, Appearance.Bare.Wearing(Wardrobe.At(901)!)),
            new WearRequest(301, CosmeticSlot.Hat),
            new TradeRequest(7),
            new TradeOffer(2),
            new TradeConfirm(true),
            new TradeCancel(),
            new TradeAsked(7, "Koop"),
            new TradeUpdated(7, "Koop", null, null, false, true),
            new TradeEnded("Trade complete!", []),
            new PlayerMoved(9, 3, 4, Direction.Down),
            new PlayerLeft(9),
            new MoveRejected(1, 1, Direction.Up, "Too fast."),
            new AuthFailed("Wrong name or password."),
            new Rejected("Log in first."),
        ];

    [Fact]
    public async Task RoundTripsEveryMessageKind()
    {
        NetMessage[] sent = SampleMessages();

        using var stream = new MemoryStream(await EncodeAsync(sent));
        var channel = new MessageChannel(stream);

        foreach (NetMessage expected in sent)
        {
            NetMessage? received = await channel.ReceiveAsync();

            Assert.NotNull(received);
            Assert.Equal(expected.GetType(), received.GetType());
            Assert.Equal(Canonical(expected), Canonical(received));
        }
    }

    /// <summary>
    /// Every message kind is in the round trip above.
    /// <para>
    /// That list is written by hand and had drifted: three kinds the server sends on
    /// every map change had never been through it. A message with no discriminator
    /// serialises as its base type and arrives as nothing at all, which shows up as a
    /// feature that silently does not happen rather than as an error.
    /// </para>
    /// </summary>
    [Fact]
    public void NoMessageKindIsLeftOutOfThatList()
    {
        var declared = typeof(NetMessage)
            .GetCustomAttributes<System.Text.Json.Serialization.JsonDerivedTypeAttribute>()
            .Select(a => a.DerivedType)
            .OrderBy(t => t.Name);

        var covered = SampleMessages().Select(m => m.GetType()).Distinct().OrderBy(t => t.Name);

        Assert.Equal(declared, covered);
    }

    /// <summary>
    /// And every kind that exists is declared, which is the half the test above does not
    /// cover and the half that actually bites.
    /// <para>
    /// That one compares the declared list against the sample list, so a kind missing from
    /// <em>both</em> passes it silently. Two were: the daycare's pair went in with a screen,
    /// a handler and a sender, and no discriminator — so every one of them would have
    /// serialised as its base type and arrived as nothing at all. It shipped in that state
    /// and the whole suite was green.
    /// </para>
    /// <para>
    /// The naming guardrail did not catch it either, because that one reads the sources for
    /// the type's name and both sides used it. Named on both sides, and unable to cross.
    /// </para>
    /// </summary>
    [Fact]
    public void AndEveryKindThatExistsIsDeclared()
    {
        var declared = typeof(NetMessage)
            .GetCustomAttributes<System.Text.Json.Serialization.JsonDerivedTypeAttribute>()
            .Select(a => a.DerivedType)
            .ToHashSet();

        string[] undeclared =
        [
            .. typeof(NetMessage).Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(NetMessage)) && !t.IsAbstract)
                .Where(t => !declared.Contains(t))
                .Select(t => t.Name)
                .Order(),
        ];

        Assert.Empty(undeclared);
    }

    /// <summary>
    /// A message reduced to its wire form.
    /// <para>
    /// Records compare their members with <c>Equals</c>, and for a list member that is
    /// reference equality — so two messages carrying identical parties are not equal
    /// to a record. Comparing the encoded form is both the thing this test actually
    /// cares about and the only comparison that means anything for a message holding
    /// a collection. Worth knowing before writing <c>==</c> on one of these in earnest.
    /// </para>
    /// </summary>
    private static string Canonical(NetMessage message) =>
        System.Text.Json.JsonSerializer.Serialize(message);

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    public async Task ReassemblesFramesSplitAcrossReads(int chunkSize)
    {
        // TCP gives no message boundaries. A frame arriving one byte at a time must
        // still be read as one message.
        byte[] encoded = await EncodeAsync(
            new PlayerMoved(1, 2, 3, Direction.Up),
            new PlayerMoved(2, 4, 6, Direction.Down));

        var channel = new MessageChannel(new DripStream(encoded, chunkSize));

        Assert.Equal(new PlayerMoved(1, 2, 3, Direction.Up), await channel.ReceiveAsync());
        Assert.Equal(new PlayerMoved(2, 4, 6, Direction.Down), await channel.ReceiveAsync());
    }

    [Fact]
    public async Task ReadsTwoMessagesDeliveredAsOneChunk()
    {
        byte[] encoded = await EncodeAsync(new PlayerLeft(1), new PlayerLeft(2));
        var channel = new MessageChannel(new DripStream(encoded, encoded.Length));

        Assert.Equal(new PlayerLeft(1), await channel.ReceiveAsync());
        Assert.Equal(new PlayerLeft(2), await channel.ReceiveAsync());
    }

    [Fact]
    public async Task ReturnsNullWhenThePeerClosesBetweenFrames()
    {
        using var empty = new MemoryStream();
        Assert.Null(await new MessageChannel(empty).ReceiveAsync());
    }

    [Fact]
    public async Task ThrowsWhenTheStreamEndsMidFrame()
    {
        byte[] encoded = await EncodeAsync(new PlayerLeft(1));
        using var truncated = new MemoryStream(encoded[..(encoded.Length - 3)]);

        await Assert.ThrowsAsync<InvalidDataException>(() => new MessageChannel(truncated).ReceiveAsync());
    }

    [Fact]
    public async Task RefusesAnAbsurdlyLargeFrame()
    {
        // A corrupt or hostile length prefix must not make the receiver allocate.
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, MessageChannel.MaxFrameBytes + 1);

        using var stream = new MemoryStream(header);
        await Assert.ThrowsAsync<InvalidDataException>(() => new MessageChannel(stream).ReceiveAsync());
    }

    [Fact]
    public async Task RefusesANegativeFrameLength()
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, -16);

        using var stream = new MemoryStream(header);
        await Assert.ThrowsAsync<InvalidDataException>(() => new MessageChannel(stream).ReceiveAsync());
    }

    [Fact]
    public async Task ConcurrentSendsDoNotInterleave()
    {
        // Two tasks writing at once must not splice their bytes together, or both
        // frames are lost.
        using var buffer = new MemoryStream();
        var channel = new MessageChannel(buffer);

        await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(i => channel.SendAsync(new PlayerMoved(i, i, i, Direction.Up))));

        using var read = new MemoryStream(buffer.ToArray());
        var reader = new MessageChannel(read);

        var seen = new List<int>();

        while (await reader.ReceiveAsync() is PlayerMoved moved) seen.Add(moved.PlayerId);

        Assert.Equal(50, seen.Count);
        Assert.Equal(Enumerable.Range(0, 50).OrderBy(i => i), seen.OrderBy(i => i));
    }
}

public class WorldDataTests
{
    private static WorldData Sample() => new(
    [
        new MapData("3.0", "PALLET TOWN", 2, 2, [0, 1, 0, 0]),
        new MapData("4.1", "PALLET TOWN", 1, 1, [0]),
        new MapData("3.1", "VIRIDIAN CITY", 3, 1, [0, 0, 1]),
    ]);

    [Fact]
    public void RoundTripsThroughTheFileFormat()
    {
        using var buffer = new MemoryStream();
        Sample().Save(buffer);
        buffer.Position = 0;

        WorldData loaded = WorldData.Load(buffer);

        Assert.Equal(3, loaded.Count);

        MapData pallet = loaded.Find("3.0")!;
        Assert.Equal("PALLET TOWN", pallet.Name);
        Assert.Equal(2, pallet.Width);
        Assert.Equal(new byte[] { 0, 1, 0, 0 }, pallet.Collision);
    }

    [Fact]
    public void FindsAMapByNamePreferringTheLargest()
    {
        // Both the town and its interiors carry the same name; the outdoor map is
        // almost always the one meant.
        Assert.Equal("3.0", Sample().FindByName("pallet")!.Id);
    }

    [Fact]
    public void BuildsACollisionGridMatchingWhatWasSaved()
    {
        CollisionGrid grid = Sample().Find("3.0")!.ToGrid();

        Assert.True(grid.IsWalkable(new GridPosition(0, 0)));
        Assert.False(grid.IsWalkable(new GridPosition(1, 0)));
    }

    [Fact]
    public void RejectsAFileThatIsNotAWorld()
    {
        using var junk = new MemoryStream(new byte[64]);
        Assert.Throws<InvalidDataException>(() => WorldData.Load(junk));
    }

    [Fact]
    public void RejectsAMapWhoseCollisionLengthDisagreesWithItsSize()
    {
        using var buffer = new MemoryStream();

        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("MONWORLD"u8.ToArray());
            writer.Write(1);      // version
            writer.Write(1);      // map count
            writer.Write("3.0");
            writer.Write("BROKEN");
            writer.Write(4);      // width
            writer.Write(4);      // height
            writer.Write(2);      // but only two collision bytes
            writer.Write(new byte[2]);
        }

        buffer.Position = 0;
        Assert.Throws<InvalidDataException>(() => WorldData.Load(buffer));
    }
}

public class RemoteCharacterTests
{
    [Fact]
    public void StartsWhereItIsCreated()
    {
        var other = new RemoteCharacter(1, "Someone", new GridPosition(3, 4), Direction.Up);

        Assert.Equal((48f, 64f), other.PixelPosition);
        Assert.False(other.IsMoving);
    }

    [Fact]
    public void WalksToANewSquareRatherThanTeleporting()
    {
        var other = new RemoteCharacter(1, "Someone", new GridPosition(0, 0), Direction.Down);
        other.MoveTo(new GridPosition(1, 0), Direction.Right);

        Assert.True(other.IsMoving);
        Assert.Equal(0f, other.PixelPosition.X);

        other.Update(WalkingCharacter.StepSeconds / 2f);
        Assert.Equal(8f, other.PixelPosition.X, 1);

        other.Update(WalkingCharacter.StepSeconds);
        Assert.Equal(16f, other.PixelPosition.X);
        Assert.False(other.IsMoving);
    }

    [Fact]
    public void AnUpdateArrivingMidWalkContinuesFromWhereItIsDrawn()
    {
        // Otherwise a fast walker visibly snaps backwards every time an update lands
        // before the previous one finished.
        var other = new RemoteCharacter(1, "Someone", new GridPosition(0, 0), Direction.Right);

        other.MoveTo(new GridPosition(1, 0), Direction.Right);
        other.Update(WalkingCharacter.StepSeconds / 2f);

        float midpoint = other.PixelPosition.X;
        other.MoveTo(new GridPosition(2, 0), Direction.Right);

        Assert.Equal(midpoint, other.PixelPosition.X, 1);
    }

    [Fact]
    public void TurningInPlaceDoesNotStartAWalk()
    {
        var other = new RemoteCharacter(1, "Someone", new GridPosition(2, 2), Direction.Down);
        other.MoveTo(new GridPosition(2, 2), Direction.Left);

        Assert.Equal(Direction.Left, other.Facing);
        Assert.False(other.IsMoving);
    }
}

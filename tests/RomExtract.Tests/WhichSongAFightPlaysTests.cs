using PokeMmo.Core.Sound;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Which song a fight plays, and — more to the point — which of those numbers came off a
/// cartridge and which one this project decided.
/// <para>
/// This is the thinnest cartridge-backed thing in the whole sound work, and the tests are
/// mostly about that thinness holding. A slot with no answer has to stay empty and stay
/// counted; a number somebody decided must never quietly become a number that was read.
/// </para>
/// </summary>
public class WhichSongAFightPlaysTests
{
    /// <summary>Nothing known is nothing said, rather than a stand-in song.</summary>
    [Fact]
    public void WithNothingKnownAFightHasNoSong()
    {
        var music = new BattleMusic();

        Assert.Equal(Jukebox.Nothing, music.For(BattleKind.Wild));
        Assert.Null(music.Of(BattleKind.Wild));
    }

    /// <summary>And every kind of fight is counted as having no song, not none of them.</summary>
    [Fact]
    public void AndEveryKindIsCountedAsSilent()
    {
        var music = new BattleMusic();

        Assert.Equal(Enum.GetValues<BattleKind>().Length, music.Silent.Count);
    }

    /// <summary>A song that was read comes back, and says it was read.</summary>
    [Fact]
    public void ASongThatWasReadComesBack()
    {
        var music = new BattleMusic([new BattleTheme(BattleKind.Scripted, 291, Read: true, "a script")]);

        Assert.Equal(291, music.For(BattleKind.Scripted));
        Assert.Equal(1, music.ReadKinds);
        Assert.Equal(0, music.ModelledKinds);
        Assert.DoesNotContain(BattleKind.Scripted, music.Silent);
    }

    /// <summary>
    /// And one that was decided says that instead.
    /// <para>
    /// The two are the same integer and different facts, which is the whole reason the flag
    /// travels with the number rather than being worked out from it.
    /// </para>
    /// </summary>
    [Fact]
    public void AndOneThatWasDecidedSaysSo()
    {
        var music = new BattleMusic([new BattleTheme(BattleKind.Wild, 12, Read: false, "a decision")]);

        Assert.Equal(12, music.For(BattleKind.Wild));
        Assert.Equal(0, music.ReadKinds);
        Assert.Equal(1, music.ModelledKinds);
    }

    /// <summary>
    /// Something decided never replaces something read.
    /// <para>
    /// The direction that matters. A decision arriving after a reading would turn a number
    /// with an address behind it into a number without one, and nothing downstream could tell
    /// — both are an int and a bool, and the bool would be the one that changed.
    /// </para>
    /// </summary>
    [Fact]
    public void ADecisionNeverReplacesAReading()
    {
        var music = new BattleMusic();

        music.Set(new BattleTheme(BattleKind.Scripted, 291, Read: true, "a script at 0x1A75E5"));
        music.Set(new BattleTheme(BattleKind.Scripted, 7, Read: false, "a decision"));

        Assert.Equal(291, music.For(BattleKind.Scripted));
        Assert.True(music.Of(BattleKind.Scripted)!.Read);
    }

    /// <summary>But a reading does replace a decision, which is the point of ever taking one.</summary>
    [Fact]
    public void ButAReadingReplacesADecision()
    {
        var music = new BattleMusic();

        music.Set(new BattleTheme(BattleKind.Scripted, 7, Read: false, "a decision"));
        music.Set(new BattleTheme(BattleKind.Scripted, 291, Read: true, "a script at 0x1A75E5"));

        Assert.Equal(291, music.For(BattleKind.Scripted));
        Assert.True(music.Of(BattleKind.Scripted)!.Read);
    }
}

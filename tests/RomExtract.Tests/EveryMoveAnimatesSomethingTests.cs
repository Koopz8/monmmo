using PokeMmo.Core.Sound;
using PokeMmo.RomExtract.Sound;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A generic hit from the first day, and a count of what is not modelled yet.
/// <para>
/// This is the same arrangement the battle engine already has for move effects, and it is
/// here for the same reason. A move whose sprite behaviour nobody has written still animates
/// — with the right timing and the right sounds, because both of those come off the
/// cartridge — and the fact that it is a fallback is <em>recorded</em>.
/// </para>
/// <para>
/// The alternative is a generic flash for every move, which is a day's work and can never
/// improve, because nothing is measuring it. The engine went from 138 silent moves to 56
/// silent groups only because there was a number to watch.
/// </para>
/// </summary>
public class EveryMoveAnimatesSomethingTests
{
    private static readonly SyntheticRom Synthetic = new();

    private static IReadOnlyList<AnimScript> Scripts()
    {
        Rom rom = Synthetic.ToRom();

        return AnimScriptReader.All(rom, AnimTableLocator.Locate(rom)!.Starts);
    }

    /// <summary>
    /// A template nobody has written a behaviour for answers "not yet", rather than throwing
    /// or quietly answering something plausible.
    /// </summary>
    [Fact]
    public void ATemplateNobodyHasWrittenAnythingForSaysSo()
    {
        var registry = new SpriteBehaviours();

        Assert.Equal(SpriteBehaviour.NotYetModelled, registry.Of(0x0810_0000));
    }

    /// <summary>And the asking is remembered, which is the whole mechanism of the count.</summary>
    [Fact]
    public void AndTheAskingIsRemembered()
    {
        var registry = new SpriteBehaviours();

        registry.Of(0x0810_0000);
        registry.Of(0x0810_0040);
        registry.Of(0x0810_0000);

        Assert.Equal(2, registry.SteppedOver.Count);
    }

    /// <summary>A template that has been taught answers what it was taught.</summary>
    [Fact]
    public void ATemplateThatHasBeenTaughtAnswers()
    {
        var registry = new SpriteBehaviours();

        registry.Learn(0x0810_0000, SpriteBehaviour.Arcs);

        Assert.Equal(SpriteBehaviour.Arcs, registry.Of(0x0810_0000));

        // And is not counted as stepped over, which is the other half of it.
        Assert.Empty(registry.SteppedOver);
    }

    /// <summary>
    /// With nothing taught, nothing animates properly — and that is the honest starting
    /// number rather than an embarrassing one to hide.
    /// </summary>
    [Fact]
    public void WithNothingTaughtNothingAnimatesProperly()
    {
        Coverage coverage = new SpriteBehaviours().Over(Scripts().Select(s => s.Templates));

        Assert.Equal(SyntheticRom.AnimCount, coverage.Moves);
        Assert.Equal(0, coverage.Animated);
        Assert.Equal(coverage.Moves, coverage.NotAnimated);

        Assert.True(coverage.TemplatesNot > 0);
    }

    /// <summary>
    /// Teaching one template animates every move that names it — which is the property the
    /// whole plan depends on, and the reason this is worth doing properly rather than
    /// quickly.
    /// </summary>
    [Fact]
    public void TeachingOneTemplateAnimatesEveryMoveThatNamesIt()
    {
        IReadOnlyList<AnimScript> scripts = Scripts();

        var registry = new SpriteBehaviours();

        uint first = scripts[0].Templates[0];

        registry.Learn(first, SpriteBehaviour.Travels);

        Coverage coverage = registry.Over(scripts.Select(s => s.Templates));

        // More than one, because templates repeat — a template used by many moves is worth
        // many moves, which is why the count is over moves rather than over templates.
        Assert.True(
            coverage.Animated > 1,
            $"teaching one template animated only {coverage.Animated} move(s), so nothing repeats");

        Assert.True(coverage.Animated < coverage.Moves, "one template animated the whole game");
    }

    /// <summary>
    /// And teaching all of them animates all of them, which is the number this project is
    /// working towards and has to be reachable.
    /// </summary>
    [Fact]
    public void AndTeachingAllOfThemAnimatesAllOfThem()
    {
        IReadOnlyList<AnimScript> scripts = Scripts();

        var registry = new SpriteBehaviours();

        foreach (uint template in scripts.SelectMany(s => s.Templates).Distinct())
            registry.Learn(template, SpriteBehaviour.Travels);

        Coverage coverage = registry.Over(scripts.Select(s => s.Templates));

        Assert.Equal(coverage.Moves, coverage.Animated);
        Assert.Equal(0, coverage.NotAnimated);
        Assert.Empty(registry.SteppedOver);
    }

    /// <summary>
    /// A move that knows what one of its sprites does and not the others does not count as
    /// animated.
    /// <para>
    /// Every rather than any, deliberately. A move that draws three things and models one of
    /// them looks wrong on the screen, not two-thirds right, and a count that called it
    /// animated would be a count that flattered itself.
    /// </para>
    /// </summary>
    [Fact]
    public void AMoveThatKnowsSomeOfItsSpritesIsNotAnimated()
    {
        var registry = new SpriteBehaviours();

        registry.Learn(1, SpriteBehaviour.Travels);

        Coverage coverage = registry.Over([[1u, 2u]]);

        Assert.Equal(0, coverage.Animated);
    }

    /// <summary>
    /// And a move that draws nothing at all is not animated either — a script with no
    /// sprites in it is a script this layer has nothing to say about, and calling that
    /// success would inflate the number for free.
    /// </summary>
    [Fact]
    public void AndAMoveThatDrawsNothingIsNotAnimatedEither() =>
        Assert.Equal(0, new SpriteBehaviours().Over([[]]).Animated);

    /// <summary>The coverage says itself in words, for the report that will print it.</summary>
    [Fact]
    public void AndTheCountSaysItselfInWords()
    {
        string said = new SpriteBehaviours().Over(Scripts().Select(s => s.Templates)).ToString();

        Assert.Contains("moves animate", said);
        Assert.Contains("not yet", said);
    }
}

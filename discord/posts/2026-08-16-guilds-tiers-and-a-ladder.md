---
channel: devlog
title: A bug that made two people own the same creature, and then the whole PvP half
ping: devlog
thread: true
---

Eight milestones. One of them was a duplication bug that nothing would have
caught, and the rest is most of what a competitive game needs.

## First, the one that mattered

Two milestones back I wrote this at the bottom of the market's page and left it
there:

> The window between the store committing and the in-memory copy catching up is
> unchanged, and it is the same window creatures have had since the market was
> built.

It was worse than that sentence made it sound.

A character is saved from a **photograph** of memory, handed to a background
writer, and developed a moment later. The market writes the same character *by
hand*, in a transaction, while its owner is still playing — that is what listing
something is.

The market's defence was to tell the writer to forget whatever it was holding.
That works right up until the writer has already taken the photograph out of the
queue and is inside the write. Forgetting clears the queue; it cannot reach a
photograph that has left it.

So: photograph taken with the creature in the box. Market commits — character
written without it, escrow row created, listing on the board. Photograph
developed on top. A save rewrites a character's lists wholesale, so the creature
comes back to the box; the escrow row lives in a different list and survives.

**The creature is now in the box and on the market.** Both halves internally
consistent. Nothing throws. Nobody finds out until two people own it.

The fix is a per-account hold the writer takes around its save — per account, not
one gate for everybody, because two players saving at once is the ordinary case.
And **the writer takes the gate before it takes the photograph out of the queue**,
which is the whole of it: reversed, it is the same bug by a slower road.

That ordering needed its own test, and working out why was the most useful thing
in the milestone. The obvious test pins a writer inside a save — but a writer
pinned *inside* the save is pinned past both the taking and the waiting, so the
order of those two is invisible from there.

## Held items, the half that does something

34 of 66 effects now do something, and the consumed half — every berry, both
herbs — has the machinery it needed.

## Guilds

A named group, a shared channel, a roster, invitations. Then a screen on **G**,
built deliberately in the same shape as the market's screen, because the market's
shape turned out to be right: a request is turned into the console line it is
equivalent to, run through the path the console runs, and the whole guild is
photographed and sent back.

**One implementation of "one guild each", two front ends.** Two implementations
of that rule is how somebody ends up in two guilds, and two front ends is exactly
the situation that invites one.

The screen says whether you lead the guild, which is what lets it *hide* invite
and kick from a member rather than offering them and refusing. A refusal that
arrives a keypress after the decision is worse than never having offered.

## A tier list you can recompute

PokeMMO has tiers and they are curated — a committee decides, and from outside
there is no way to check the list against anything. That's a reasonable way to
run a competitive game and it's the opposite of what this project can offer, so
this does the other thing.

**Read:** every species' six base stats, and therefore their total.
**Decided:** that there are five bands. That is the whole of it.

The boundaries aren't written anywhere in the source — they're the quintiles of
the totals of the species *this cartridge actually fields*. A different image
with different creatures produces different boundaries without a line changing,
and there's a test that hands it a different set and asserts every boundary
moves. A number written in the source would sit still.

```
411 species with stats, in 5 bands at the quintiles of their totals
boundaries: 305, 390, 455, 515

Fledgling     87 species, 180..305
Rising        80 species, 308..390
Seasoned      80 species, 395..455
Formidable    84 species, 456..515
Legendary     80 species, 518..680
```

The names are plain words rather than borrowed ones — there's a test asserting
none of them are PokeMMO's, because calling a band "OU" would be claiming this
was their list.

**A party is where its strongest member is**, not its average. Five weak
creatures and one that wins on its own is a party that wins on its own.

And the placeholder trap, which matters more than it sounds: the cartridge's stat
table is longer than its list of creatures, and counting the slack as very weak
things would drag every boundary down — silently, by an amount nobody would
notice without recomputing.

## A ladder, one per band

Elo. Not because it's familiar, but because it's the only rating system where
somebody can be told **why** their number moved by the amount it did. Two ratings
in, one probability out, and the change is the difference between what happened
and what was expected. A player who disagrees can check it with a calculator.

Start 1000, swing 32, scale 400. The scale isn't a tuning knob — it's the unit
the system is measured in, and a test says what it means: four hundred points is
ten wins in eleven.

**One rating per band.** A single overall number would average two abilities that
never meet — somebody very good with weak creatures and hopeless with strong ones
has two skills, not one. It also kills the obvious abuse: a strong party can't
farm the bottom of the ladder, because it isn't on that ladder. The band a duel
counts in is the **higher** of the two parties.

Both halves are computed against the pair as it stood before either changed, in
one transaction. Otherwise the two halves stop adding up — silently, a few points
at a time, in the direction that inflates the whole ladder.

Two things worth reading twice. **The second test didn't catch it the first
time**: it checked that a rating in one band reads as unstarted in another, which
goes through a different query from the one a *recording* uses, so the break
sailed past it. And **one thing is stated but not proved** — that a result is
taken exactly once. Breaking it leaves every test green, because reaching it
needs a real duel driven to a finish through the world. That's written in the
source and said out loud here rather than left to be assumed.

Nothing yet *refuses* a duel across bands. Measure first, then decide whether to
forbid.

## More than one box

Eight of them. There is nothing on the cartridge to derive the count from, so
it's modelled, and both the number and the reason live in one place: enough that
filling it is a project rather than an afternoon, few enough that a full one is
still a list somebody can scroll.

The locator that went looking is tested against images that **do** contain what
it's hunting, at three lengths and two strides. That's the part that would
otherwise rot — a locator that always returned nothing would pass every test
written against this cartridge, because the negative is what this cartridge
gives, and would then quietly answer "one box" on a cartridge that had twenty.

## Where that leaves things

**1938 tests.** Against PokeMMO's list, what's actually missing is now short:
four more regions (blocked on cartridges this project doesn't have), the
cartridge font (four methods defeated), a ladder screen, whether a duel should be
refused across bands at all, and the thousand-player measurement — still the only
open item blocked on something that isn't code.

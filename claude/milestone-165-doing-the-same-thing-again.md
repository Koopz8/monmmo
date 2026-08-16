# Milestone 165 — Doing the same thing again

Six groups, and a mistake I made in the middle of them that is worth more of this
document than the moves are.

## What the six are

**FURY CUTTER** and **ROLLOUT** climb. Every turn running they are used, they double.
That is the first power in this engine that depends on a turn other than the one being
resolved, and it is the reason the count could not live in `MovePower` where the rest
of the odd powers live: `MovePower` is handed an attacker and a defender and a move,
and none of those three can see last turn. Only the battle can.

So the count is kept by the battle, beside the slot it was counted for. Three things
can happen and exactly one of them is subtle:

- the same slot again, and the count climbs;
- one of these moves fresh, and the count starts at nought;
- **anything else at all, and the count is gone.**

Not paused. Gone. That third case is the whole character of these moves — a climb you
can be knocked off is a gamble, and a climb that waits for you is a ramp, and they play
nothing like each other. It is one `else` and it is the most load-bearing line in the
milestone.

ROLLOUT adds a lock: once it starts it does not let go. The lock is started only at the
bottom of the climb, because a lock renewed every turn is a lock that never ends — that
is a bug I did not write, only because the endless-lock version of it was written and
tested three milestones ago on THRASH and the shape was already familiar.

The cap on the doubling is **modelled**. Nothing on any record says how far these
climb, and nothing that doubles without a ceiling belongs in a game people play for
months. So there is a number, it is named, and the test asserts that a ceiling *exists*
rather than that it is four. That distinction is the project's usual one and it is
worth keeping sharp: a test that pinned four would be a test of my opinion.

**TRIPLE KICK** is three hits whose count is fixed and — uniquely in this game — whose
hits are not all worth the same. Which go it is had to reach the damage sum, so
`DamageCalculator.Calculate` grew a `hit` argument that every other move in the game
ignores. It multiplies rather than doubles: one, two, three of the power rather than
one, two, four. A climb, not a doubling, and at these numbers the difference is the
whole feel of the move.

**PSYCH UP** takes every one of the other one's stat changes and replaces its own with
them. Both halves are tested, because both halves are how it could be quietly wrong: a
version that took only the good ones would be a move nobody could play around, and a
version that added rather than replaced would turn two of these into six stages of
anything.

**MUD SPORT** and **WATER SPORT** turn one type down for the room. That makes them the
second fact in this engine that belongs to the battle rather than to either side of it
— the sky was the first, and until the sky there was nowhere for a fact about the room
to live at all. Damping is therefore the *last* multiplier on the damage, after the
ability, after the weather, after the wall: it is the only one that neither creature
owns. Somebody who turned the electricity down turned it down for themselves too.

Which type each of them damps rides on the effect's `Stat` field — Speed means
electricity, Attack means fire. That is a field being used for something other than its
name, which is worth saying out loud rather than leaving to be discovered. The
alternative was a type column on the effect table that would be null for three hundred
and fifty-two moves.

## The mistake

I wrote a shell helper to break each new guard and confirm the right test caught it —
the usual close-out for a milestone here. The helper restored each file afterwards with
`git checkout -- <file>`.

Nothing had been committed yet.

`git checkout` on a file with uncommitted changes does not restore it to how it was a
moment ago. It restores it to `HEAD`. The first break-and-restore silently threw away
every uncommitted change in `Battle.cs`; the next two did the same to `MovePower.cs`
and `DamageCalculator.cs`. Three files, the entire source half of the milestone, gone
in about four seconds — and gone *quietly*, because the helper's own output for the
first break looked exactly right: two tests failed, which is what it was there to show.

What survived: the tests, the effect table, the narrator, and `Battler`. The helper
never touched those, so the contract was completely intact even though the
implementation was not. Rebuilding was rework rather than guesswork — the fourteen
tests said precisely what the three files had to do again, and the full suite came back
to 2080, the same total as before the loss, which is the only reason I can say the
rebuild is equivalent rather than merely green.

Three things worth keeping from it:

**The discipline was right and the tool was wrong.** Breaking every guard on purpose is
a good practice and I am not dropping it. Doing it against an uncommitted tree is what
made it dangerous, and that is fixable in one line.

**Commit first, then break things.** The pass now begins by asserting the tree is
clean and refusing to run if it is not. That check is the actual fix, and it is three
words long.

**It looked fine.** This is the part that connects to everything else in this project.
The failure mode was not an error message, it was a plausible result — the same shape
as overwriting eight THIEF tests and watching the suite go green, and the same shape as
the type multiplier that turned a zero into one point of damage and looked like a hit.
Every serious mistake in this project so far has been something that looked like it
worked. The countermeasure has always been the same one: count something, and check the
count against a number you knew beforehand. 1844 + 19 ≠ 1855 caught the tests. 2080
before and 2080 after is what says this rebuild is done.

## The guards, and what happens when each is broken

All ten run against a clean tree now, and each was confirmed to be caught by name:

| Broken | Caught by |
|---|---|
| the count climbs | `ItGetsHarderEveryTimeItIsUsedRunning`, `AndUsingSomethingElseStopsIt` |
| the count is cleared by anything else | `AndUsingSomethingElseStopsIt` |
| the doubling | `ItGetsHarderEveryTimeItIsUsedRunning` |
| the cap on the doubling | `AndItStopsDoublingSomewhere` |
| the lock while it rolls | `TheRollingOneTakesTheChoiceAway` |
| three goes rather than one | `ThreeGoesEachHarderThanTheLast` |
| each go harder than the last | `ThreeGoesEachHarderThanTheLast` |
| the stage copy | `CopyingTakesEveryStageAndNotOnlyTheGoodOnes` |
| damping reaches the damage | `EachDampsOneTypeAndLeavesTheOther` (both) |
| damping is remembered by the room | `EachDampsOneTypeAndLeavesTheOther` (both) |

## A test that was measuring the wrong thing

`ItGetsHarderEveryTimeItIsUsedRunning` went through four versions before it measured
what it claimed to.

It began by comparing four rolled damage numbers — but damage carries an
eighty-five-to-a-hundred roll, which at these powers is worth more than one doubling.
It was comparing noise. Then it compared `MovePower` values and got "80 should beat
80", because it had not advanced the count. Then it asserted `[0, 1, 2, 3]` and got
`[0, 1, 2, 2]`, and *that* one is the interesting failure: the fourth turn had finished
the deliberately papery defender the file uses, and a finished fight does not take
turns. The count had stopped climbing because there was nothing left to hit.

The first fix was to drop to three turns, which made it pass — and would have shipped a
test that passed for a reason unrelated to its name. The real fix is a second fixture:
a defender built to last, used by the three tests that count turns, while the papery
one stays for the tests that measure a difference. Plus `Assert.False(battle.IsOver)`
said out loud, because every one of those counts is a count of turns that *happened*,
and a fight that ended early gives the same rising list right up to where it stopped.

## What is not stated here

The move-coverage figures. This batch removes six groups from the silent list, and what
that comes to in moves is a report run against a cartridge — which this machine does
not have and will never have. I put extrapolated numbers in the commit message, noticed,
and amended it. The last measured figures remain 283 of 354 moves and 115 groups down
to 62; the next real numbers come from a run against a player's own file.

## Still silent

Copies (METRONOME, MIRROR MOVE, SLEEP TALK, MIMIC, SKETCH, ASSIST, ROLE PLAY, SKILL
SWAP). Mutable type (CONVERSION, CONVERSION 2, CAMOUFLAGE, WEATHER BALL). Out-of-turn
(COUNTER, MIRROR COAT, REVENGE, FAKE OUT, FOCUS PUNCH, VITAL THROW). Party-reaching
(HEAL BELL, BATON PASS, PURSUIT). And the large three: SUBSTITUTE, BIDE, FUTURE SIGHT.

LOW KICK stays silent until species weight is located — it lives on the dex table
rather than the base-stat record, and it is a locator of its own rather than a move
group.

2080 tests.

# Milestone 167 — Finishing the silent half

Every remaining family on the list. Twenty-three groups across five batches, and the last of
them closes the gap that has been the top item on this project's roadmap since milestone 145.

## What was left, and what each actually needed

**Seven that do nothing themselves.** MIRROR MOVE, METRONOME, SLEEP TALK, MIMIC, SKETCH,
ROLE PLAY, SKILL SWAP. Two new things: a creature that remembers *which move* it last used
rather than which slot, and an ability a fight can change. The slot was never enough — a slot
indexes one creature's own four, and the thing MIRROR MOVE wants is the move the *other* one
used. Borrowing is one call back into the turn with the move it chose, and only ever one:
without that, METRONOME picking METRONOME never comes back.

SLEEP TALK needed the sleep check itself to change, since it is the one move in the game
whose whole point is that it works while its user cannot act.

**Six that answer what already happened.** COUNTER, MIRROR COAT, REVENGE, FAKE OUT, FOCUS
PUNCH, VITAL THROW. The roadmap called this family "needs to act out of turn" and that was
wrong in the most useful way: **the ordering was already there.** Priority is a signed byte on
the move's own record, read off the cartridge since moves were first read, and this engine has
ordered by it since long before any of these had an effect. All that was missing was two
fields of memory.

**Four about being something you were not born.** CONVERSION, CONVERSION 2, CAMOUFLAGE,
WEATHER BALL. One field, not two: all three of the user-changing ones make their user *one*
type and nothing else.

**Three that needed machinery of their own.** SUBSTITUTE, BIDE, FUTURE SIGHT. These were
genuinely large. A stand-in that absorbs the *whole* of the blow that breaks it; an
accumulator that spends two turns collecting and gives back double; and the first thing in
this engine that outlives the turn that made it and belongs to neither creature.

**Three that reach past the field.** HEAL BELL, BATON PASS, PURSUIT.

## The pattern, now confirmed five times

Before this milestone the note in `finishing-firered.md` read: *twice now, the largest part of
a silent list has been groups whose machinery already existed and which nobody had connected.*

It is five now. Of the twenty-three groups here, **fourteen needed no new machinery at all** —
they needed a line pointing at something the engine had. The nine that needed something new
needed far less than their names suggested: two int fields for the whole out-of-turn family,
one nullable enum for the whole type family.

The generalisation worth keeping: **a family named for the machinery it appears to need is
usually named wrong.** Check what it needs against what is already there before building
anything. The naming comes from how the moves *feel* to a player, and how a move feels and
what a move costs to implement are unrelated.

## What a battle owns, and what it does not

Four things now arrive from outside: the move a creature is left with, the whole move table,
what the ground is made of, and each side's party. The pattern is the same every time — a
battle works in the two creatures standing on the field, and anything beyond them is handed
in rather than reached for.

BATON PASS splits along that seam exactly. What a creature *built* is the battle's to keep,
and it keeps it. Bringing somebody in is not, so the handover is a method somebody else calls.
What travels is the stages and the things started on the creature; what stays is what was
*done to* it, because a condition is not something its owner built and passing one on would be
handing over a problem rather than an advantage.

## Ten guards nothing could fail

The break-the-guard pass found **nine** rules that no test could fail, plus one it stopped
from being written. Running total for this project, and each one looked completely fine:

| Guard | Why nothing was watching |
|---|---|
| a sample's loop flag has two legal values | no fixture had an illegal one |
| a song must name a voicegroup that was found | no fixture had one that did not |
| a table entry must name a song | one table-shaped thing existed |
| a table entry's group number is doubled | same |
| the longest run of entries wins | same |
| a loop keeps its fraction | every loop test used an integer rate |
| the high half of a byte is read first | both fixtures used equal halves |
| the borrowed type reaches the damage | the test compared two damage numbers |
| gathering gives back *twice* | the test used `>=` where the claim is `==` |
| catching a leaver goes first | **removed** — see below |

The last is the one worth reading twice. A rule giving PURSUIT first place against a switcher
was written, and breaking it broke nothing — because **a switch is never resolved inside the
battle.** The server does both switches before it calls in, precisely because a switch is not
a turn, so by the time an order is decided there is nobody left to go before.

The rule was removed rather than propped up with a test written to fit it, and a paragraph
explaining why sits where somebody would otherwise put it back. Making the order matter means
moving the switch inside the turn — a change to how a duel is run, not a line in the engine —
and that is written down as not done rather than quietly assumed.

**That is the first of the ten caught before it was written rather than after.**

## The two mistakes

**A revert script, again.** Breaking a guard restores with `git checkout`, which restores to
`HEAD` rather than to a moment ago. On a dirty tree the difference is every uncommitted change
in that file, gone in four seconds and gone quietly — because the output of a successful break
looks exactly like the output of a successful break. It cost the source half of milestone 165
the first time and one edit the second. The check is no longer advice in a comment:
`tools/break-guard.sh` refuses on a dirty tree.

**And then that script failed silently.** It looked for `dotnet` under `$HOME/.dotnet` and ran
as a user whose home is elsewhere, so every invocation started nothing — and reported that as
"the run produced no summary", which the script's own message then explained as a guard being
caught loudly. A tool for noticing silent failures, failing silently, and interpreting its own
silence as a result. Three breaks were read as evidence before it was checked.

## Fixtures that were wrong before the code was

Five tests failed for reasons that were correct behaviour answering a question the test was
not asking: a creature that was still part-Normal so had nothing to become; a defender that
fainted before the slow creature could act; a stand-in destroyed on the turn it went up; a
gatherer slower than its attacker so the first hit landed before it began; and a creature
asleep being asked to ring a bell.

None of these were engine faults and all five looked like them. The habit that catches it is
reading the failure before changing anything — and the one that does not is assuming a red
test means the code is wrong.

## What is not stated here

The move-coverage figures. This batch removes twenty-three groups from the silent list; what
that comes to in moves is a report run against a cartridge, which this machine does not have.
The last measured figures remain 283 of 354 moves and 115 groups down to 62. The next real
numbers come from a run against a player's own file.

## What is left on the FireRed list

The silent half is done. What remains from `finishing-firered.md`:

- **LOW KICK**, which needs species weight — on the dex table rather than the base-stat
  record, and a locator of its own rather than a move group.
- Nineteen warps leading to maps that are not exported. One measurement, not yet taken.
- Whether all three obstacle-clearing moves are obtainable by playing. One measurement.
- The cartridge font. Four mechanical searches defeated; the oldest open question here.
- Ten held-item effects, every one about something outside a fight.
- Thirty-two abilities, which now have the mutable ability, mutable type and end-of-turn hooks
  they were waiting for.
- **PURSUIT's ordering**, which needs the switch moved inside the turn.

2274 tests.

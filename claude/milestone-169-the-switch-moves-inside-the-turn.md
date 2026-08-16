# Milestone 169 — The switch moves inside the turn

Switching in a duel already worked. That is the whole problem with it: it worked, and four
separate things were quietly falling out of it, and 2451 tests had nothing to say.

## Two callers of one piece of machinery, one of them complete

Sending somebody out used to mean building a whole new battle around the creature that had
not moved, keeping only where the dice had got to. Two places did that: `Encounter`, for a
fight against the game, and `Duels`, for a fight against a person.

`Encounter` remembered what a rebuild costs. `Duels` did not.

```
a duel, one turn of RAIN DANCE
  before the switch : sky Rain, 4 turns left
  after  the switch : sky None, 0 turns left

and the same thing when somebody faints rather than chooses:
  before SendNext   : sky Rain, 4 turns left
  after  SendNext   : sky None, 0 turns left
```

The note on `ContinueFrom` has said since the day it was written that *a rain that stopped
because somebody swapped a creature would be a five-turn rule anybody could cancel for free*.
It said it from the one caller that never called it. And the second half is worse than the
first: replacing somebody who has fainted comes through the same method, so the weather
stopped on a switch **nobody chose**.

### Four things, one seam

| What | How long it had been true |
|---|---|
| a duel's weather stops on any switch, and on any faint | since duels existed |
| `ContinueFrom` carries the sky and not the room's damping | since damping existed |
| `Arrival` is never called in a duel — INTIMIDATE, DRIZZLE, DROUGHT and SAND STREAM do nothing in PvP, including on the opening send-out | since duels existed |
| **nothing anywhere sets `PlayerParty` or `OpponentParty`** | since they were written |

The last is the one worth sitting with. Those two properties exist for the one move that
reaches past the field, and no caller in the entire server has ever set either. HEAL BELL has
been reaching an empty list in every fight ever played. Its tests pass, because its tests
supply the party by hand — which is the same shape as the test called `TheSkyOutlivesASwitch`
that passes today: **it calls `ContinueFrom` itself.** Nothing anywhere checked that a caller
calls it.

That is now the twenty-ninth guard this project has found that nothing could fail, and the
first one where the missing thing was not a rule but a *call*.

## Not fixed by adding the missing call

A duel does not rebuild the battle any more. `Bring` swaps the creature in place, so the room
is never torn down and there is nothing left to carry across.

The distinction matters more than it reads. Adding `ContinueFrom` to the second caller fixes
this instance; removing the rebuild removes the category. Anything added to `Battle` tomorrow
that belongs to the room is carried through a duel's switch for free, because nothing happens
to it at all.

`Encounter` still rebuilds — a fight against the game holds the trainer's bench but not the
player's, since a party is `SavedMon` on the save until somebody is restored into a `Battler`
— so `ContinueFrom` is still load-bearing there and now carries both fields. The guard on it
is not "the call is made"; it is **"every field the room owns is carried"**, because a rule
with one of its two fields copied looks exactly like a rule that works.

## The rule milestone 167 removed, put back

167 wrote a rule giving the move that catches a leaver first place, broke it on purpose,
found that nothing failed, and **took it out rather than propping it up with a test written
to fit it** — leaving a paragraph where somebody would otherwise put it back. The reason:

> a switch is never resolved inside the battle. The server does both switches before it calls
> in, precisely because a switch is not a turn, so by the time an order is decided there is
> nobody left to go before.

So the switch moved inside the turn. `Battle.Player` and `Battle.Opponent` became settable
from inside the class, `TakeTurn` handles a `SwitchTo` by bringing somebody in off the bench
the engine is handed, and `PriorityOf` gained two lines:

- a switch outranks every move, because leaving is not something the other side interrupts;
- the move that catches a leaver outranks the switch — **and only against somebody who has
  actually said they are going.**

That second half is the one a fixture with only a leaver in it cannot tell from a free first
strike every turn, so there are two tests and the second is the one that matters.

The paragraph in `PriorityOf` explaining why the rule was absent has been replaced by the
rule, and by a note saying it is back and what made it observable.

## What leaving the field costs

The games' own rule, which this engine did not have: a creature that leaves lets go of what
it built. Stages, and the things true only while standing there. Without it, switching out and
back in was a way of banking a boost.

The line is the one `Passed` already drew for BATON PASS and it is now written once, on
`Battler.LeaveTheField`: **what a creature built ends when it leaves; what was done to it
travels with it.** A condition, a count of turns asleep and what it is carrying are facts
about the creature rather than about the square.

Four things are deliberately *not* in there — mist, safeguard and the two screens. Those
belong to a side rather than to a creature, and they live on `Battler` only because there is
nowhere for a side's state to live yet. A switch ending a screen the whole team is under
would be wrong. Written down as a modelling limit rather than quietly corrected.

## A trap, removed and replaced by a rule

The single-player path did this:

```csharp
// A switch is not a move ... what reaches the engine is a side that does
// nothing this turn, which is exactly what a switch costs.
```

and then handed the engine the switch unchanged. It was harmless only because nobody had ever
set a bench, so the engine's own switch found nobody to bring in.

Neutralising the action turned out to be the wrong fix — a side handed `UseMove(0)` *attacks*,
and a test caught that immediately. So the accident became a stated rule instead: **a battle
given no bench for a side does nothing when that side asks to switch**, with a test named
after it. Wiring a bench in later can no longer silently swap twice.

What it costs is that the catching move cannot catch a player switching in a trainer fight.
That is written down as not done rather than assumed away.

## Nine guards, one of which nothing could fail

Every new rule broken on purpose. Eight failed a named test at once. The ninth:

| Guard | Why nothing was watching |
|---|---|
| somebody who cannot fight cannot be brought in | the server filters fainted slots before the engine sees them, so every test that asked for a switch had already been filtered |

The engine is the last thing between a client and the field, and a check above it is a
courtesy rather than a wall. Decoy added; it fails now.

## Read and modelled

**Read.** Nothing new. Every number in here was already on the cartridge or already decided.

**Modelled.** That a switch outranks every move, and that the catching move outranks a switch
— both the games' arrangement rather than anything in a table. Where a switch sits numerically
(seven, chosen only to win). What a creature loses when it leaves the field, and the four
side-scoped things left out of that list.

## What this owes

- **The four side-scoped fields** — mist, safeguard, reflect, light screen — need somewhere
  for a side's state to live that is not a creature.
- **`PlayerParty` in a fight against the game.** The player's bench is `SavedMon` until
  restored, so HEAL BELL still reaches only the other side's party there. Half-wired would
  look exactly like whole.
- **The catching move against a player switching in a trainer fight**, which needs that same
  bench.

2465 tests.

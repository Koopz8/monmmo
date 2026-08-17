# Milestone 179 — Ninety-four fights won and not one level gained

Delivered as `claude-215.bundle` on the tip of `214`. 2688 tests green from a clean clone at
the base. Measured against the cartridge.

## The measurement that found it

`--play` printed `highest level 25` on every one of six passes of a run that won ninety-four
fights. That line had read the same for as long as it existed. One line per creature says why:

```
the party: #131 at 25, #133 at 25, #131 at 25, #131 at 25, #131 at 25, #131 at 25
```

Six gift creatures, all at twenty-five, none of which ever gained a level. **A summary is where
a fact goes to hide.**

## The fault

`BattleFactory.Save` says it in its own note:

> *Experience is not among the things a battler carries, so what comes back here has none. That
> is right for something just caught… and wrong for anything that already had a save, **which
> is why every caller starting from one puts its experience back afterwards**.*

There are four callers. Three hand over something just caught or just given, and are right to
have none. The fourth is the autoplayer writing its party back after every fight:

```csharp
for (var i = 0; i < mine.Count && i < party.Count; i++) party[i] = BattleFactory.Save(mine[i]);
```

It did not put it back. So every win reset the total to nothing, and the next award computed
`max(0, TotalForLevel(level)) + gained` — the experience for one fight above the threshold the
creature was already standing on, which no single fight ever crosses. **The party could not
level up. Ever.**

Nothing failed. A party that does not grow fights every battle correctly and loses the ones it
should lose, and the loss count is reported as a fact about the cartridge.

Fixed at the seam rather than the call site: `Save(battler, before)` is the line the note says
every caller writes, made into something a caller cannot forget to write.

## What moved

| | before | after |
|---|---|---|
| highest level, six passes | 25 | **40** |
| fights won | 94 | **108** |
| fights lost | 63 | **49** |
| maps reached | 179 | 179 |
| flags | 139 | 139 |

**The floor did not move**, and that is the honest headline. Fourteen more fights won, fourteen
fewer lost, and not one new map — including SAFFRON, where the run still loses to GIOVANNI at
level 40 with a party of five LAPRAS and a EEVEE.

## And the line that says these numbers are not a floor

```
the party: #131 at 40, #133 at 30, #131 at 28, #131 at 25, #131 at 25, #131 at 25
4 of those are a second copy of something already in it — a gift taken
again on a later pass. THIS RUN IS NOT A FLOOR IN THAT RESPECT.
```

Every pass runs every script again, so a gift the cartridge hands over once is taken once per
pass. Five LAPRAS is a party no player could assemble — the run has been getting **more** than
a player would, in the one dimension where every other number here is a floor. That is printed
now rather than left in the list to be noticed.

**And the starter is not in the party at all.** Six members, five LAPRAS and a EEVEE, and
nothing from the professor's ball — whose species comes out of a variable this walk cannot
resolve. So the run plays the whole game on gifts.

Both of those are measured and neither is fixed. They are the next thing.

## The guards

**Three breaks, three caught.** The party written back without what the battle could not carry;
the seam handing back the battler's own absent experience; and an award no longer accumulating.

The behavioural guard is exact rather than approximate, and it had to be: *two wins are worth
more than one*. Everything weaker passes without the fix — after a single fight the total is
right either way, because the award is the last write. It is the second fight that either adds
to the first or replaces it, and that is the only statement the bug can be caught by.

The first attempt at it used a second trainer that does not exist in the fixture rules, won one
fight instead of two, and failed for the wrong reason. Worth writing down: a behavioural guard
that fails is not automatically a guard that works.

## What is next

* **The starter never joins.** `givemon` with the species in a variable, and nothing resolves
  it. A whole party slot, and the only creature in the game a player chooses.
* **The gift taken once per pass.** A ceiling sitting inside a floor. Whatever guards it in the
  cartridge is not being honoured here — a flag the script sets, most likely, on an arm this
  walk takes differently.
* **`0x3F`, `0xE6`, `0xC0`, `0xA7`** — the unknown commands ranked by what is behind them.
* **The four that no width reads on from** — `0x92`, `0x9B`, `0xD3`, `0x62`. Misreads, not
  missing widths.
* **The other five wall flags**, and the ~28 unguardable enumerations in `Program.cs`.

## Still open, unchanged

Held items; signs never run; `--say-yes` costing party members; the nine `ARRIVED ON AN
ISLAND`s; eleven maps with no way in; shortest-chain ways in; `Bag.PocketCapacity` in shipped
saves; money modelled; `SpecialContracts.ComparedAfter`; co-op step 4; `StoryClosure` as the
no-bag control; `MapScripts` with no coverage at all; milestone docs for `StoryClosure`,
`Autoplayer` and `SpecialContracts`; sound; and whether `Reachable` should honour a trigger's
own condition.

# Milestone 180 — Six was the fault, not the cost

Delivered as `claude-216.bundle` on the tip of `215`. 2688 tests green from a clean clone at
the base. Measured against the cartridge.

## The note that was backwards

The roadmap has carried this for several milestones, in the "open, and honestly owed" list:

> `--say-yes` costs party members: 6 on the floor, 2 with it on. Nothing says so out loud.

It reads as a price — answer the questions and you lose four creatures. It is the opposite.
**Six was the fault. Two is the game.**

SILPH CO. `1.53` person 2:

```
6A 5A                lock, faceplayer
2B 46 02             checkflag 0x0246
06 01 8D 1B 16 08    goto_if set -> the "you already have one" block
0F 00 66 62 17 08    "I want you to have this POKeMON for saving us."
16 01 40 83 00       setvar 0x4001, 131          (LAPRAS)
79 ...               givemon
...
                     "Do you want to give a nickname to this ...?"   <- the run stops here
...
0x161B88             setflag 0x0246              <- on the far side of it
```

A run that never answers the question never reaches the `setflag`. The next pass finds the flag
clear and runs the whole thing from the top. **Five LAPRAS**, and the run reported a party of
six as its floor.

So a hanging question is a floor in one direction and a **ceiling** in the other, and nothing
had ever said so. `--play` says it now, at the place it prints the count:

```
218 reachable script(s) across 29 map(s) stopped at a yes-or-no and were never answered
  every one of those runs again from the top on the next pass, because the
  flag that would stop it is past the question — so whatever they hand over is
  handed over once per pass. THAT IS A CEILING INSIDE THIS FLOOR.
```

## The numbers, with the two changes separated

|  | `--play` | `--play --say-yes` |
|---|---|---|
| maps | 179 of 425 | **211** |
| flags | 139 | **176** |
| field moves | 19 | **25** |
| things carried | 64 | 79 |
| party | 6 (four of them duplicates) | **2** — LAPRAS 49, EEVEE 46 |

And milestone 179's experience fix, isolated on the same `--say-yes` run:

| | before | after |
|---|---|---|
| fights won / lost | 123 / 97 | **170 / 50** |
| highest level | 25 | **49** |
| maps | 211 | **211** |

**Forty-seven losses became wins and not one new map opened.** Levels are not what is gating
reach — which is exactly what milestone 179 could not say, because it had no `--say-yes`
comparison to say it against. Worth the extra run: a fix whose headline number does not move is
either useless or measuring the wrong thing, and this one is measuring the wrong thing.

## What this changes about every number in the roadmap

`--play` alone is **not a floor**. It is below the floor on reach — everything past a question
is unvisited — and above it on anything a hanging script hands over. Those are not two small
errors in the same direction; they are opposite, and the run has been quoted as a floor
throughout.

The honest floor is `--play --say-yes` on everything a question guards, and yes is still
**modelled** — nothing on the cartridge says a player answers yes. The difference is that the
alternative is not "no", it is "the same scene forever".

## What is next

* **The starter still never joins.** Two in the party, both gifts. `givemon` takes its species
  out of a variable and nothing resolves it — the only creature in the game a player chooses.
* **211 of 425, and levels are not the gate.** 50 fights still lost, 59 never fought at all,
  25 field moves. The frontier is the next measurement and it is now measurable, because the
  run is no longer distorted in two directions at once.
* **`0x3F`, `0xE6`, `0xC0`, `0xA7`** — the unknown commands ranked by what is behind them.
* **The four that no width reads on from**; **the five wall flags**; **the ~28 unguardable
  enumerations in `Program.cs`.**

## Still open, unchanged

Held items; signs never run; the nine `ARRIVED ON AN ISLAND`s; eleven maps with no way in;
shortest-chain ways in; `Bag.PocketCapacity` in shipped saves; money modelled;
`SpecialContracts.ComparedAfter`; co-op step 4; `StoryClosure` as the no-bag control;
`MapScripts` with no coverage at all; milestone docs for `StoryClosure`, `Autoplayer` and
`SpecialContracts`; sound; and whether `Reachable` should honour a trigger's own condition.

**Removed from that list**: "`--say-yes` costs party members". It never did.

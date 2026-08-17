# Milestone 196: the key was wrong, and nobody was asking

194 fixed the walking and left an instruction: *12 blocks are reached from more than one map,
so anything else in this repository keyed on a script address alone is wrong about them. That
is a grep worth doing rather than a guess.*

The grep found one thing. `Attempt.Ran` — the run's record of every script it ran — was keyed
on the address with no map beside it. It is fixed, it is guarded, and **it moves nothing.**

Not because the cartridge has no shared blocks. Because the only code that reads `Ran` is asked
about one setter.

---

## Two things the next-task line got wrong

The task was stated as: *`--flags` uses `played.Ran.ContainsKey` to decide whether a script ran
at all, so a nurse run on one map reads as run on nineteen.*

**`--flags` does not run a playthrough.** It is `case "--flags": flagGates = true`, which reaches
`WriteFlagGates(rom)` — one parameter, and it is the ROM. There is no `Attempt` anywhere in it.
Running `--flags` before and after this change gives byte-identical output, and the reason is
not that nothing moved: it is that nothing looked.

That is trap 1, and it is written down at the top of the brief that contained the wrong claim:
*the output is byte-identical to a scan that looked and found nothing.*

The real consumer is `WriteWhatItIsWaitingFor`, called from **one** site — the wall list inside
`--play`, and only for a blocker who was talked to and whose script did nothing.

---

## The fault was real

```
                                            places ran   distinct blocks   ran on >1 map
--play                                            781           722              7
--play --say-yes                                 1077           987             10
--play --say-yes --in-order                       958           936              2
--play --say-yes --boat                          1478          1283             17
--play --say-yes --boat --in-order               1295          1212              9
--play --say-yes --boat --surf --in-order        1295          1212              9
```

Seven to seventeen blocks per run were one dictionary entry where they are several scenes, and
between 22 and 195 places were folding into them. The merge is `.And(did)` — a union of what
each pass managed — so a block that stopped at a yes-or-no in one town carried that reason for
every other town it hangs off.

## And it reaches the output nowhere

Every `--play` run at every one of the six lever settings is **byte-identical before and after,
apart from the lines this milestone adds.** Reach did not move: 183, 243, 243, 381, 381, 381.
No flag moved. No verdict in the wall list changed.

The instrument that says why is the tally, and it is the whole point of adding it:

```
    of 1 setter(s) the list above asked about:
         1  OnAMapItNeverReached
         0  ItRanTheScriptHere
         0  ItRanTheSameBlockOnAnotherMap   <- nought, so no verdict here moved
         0  ItNeverRanTheScript
```

**One.** At the floor, at `--say-yes`, and at `--say-yes --in-order`, the only consumer of `Ran`
in this repository is asked about exactly one setter, and the answer is that the map was never
reached — so `Ran` is not even consulted. It is CERULEAN CAVE's guard, `0x005C`, set by `32.0`
ONE ISLAND person 3, which milestone 190 already closed.

At all three boat settings the number is **zero**: the run reaches ONE ISLAND, the door opens,
and the wall list asks nothing at all.

So: a dictionary that has been wrong for nineteen milestones, in a way that would have printed a
confident reason borrowed from another town, and it never got the chance. That is not a reason
to leave it wrong — the next thing to read it would have inherited the fault silently — but the
honest headline is **the key was wrong and nobody was asking**, and only the denominator can say
that. Without it, `0` and `nothing asked` print the same.

---

## What changed, in the code

| | before | after |
|---|---|---|
| `Attempt.Ran` | `IReadOnlyDictionary<uint, WhatRan>` | `IReadOnlyDictionary<(string MapId, uint Address), WhatRan>` |
| the verdict | a three-way conditional in `Program.cs` | `Attempt.HowItStands` → `WhereItStands`, four answers |
| the denominator | — | `Attempt.RanAnywhere`, derived from `Ran` |

The verdict moved out of the printer because **a rule about the world in a file no test can
reach is a rule nothing can fail** — the sixth time this project has moved the same kind of line
out of `Program.cs`, and the first four of this milestone's five breaks would have come back
green if it had stayed there.

`RanAnywhere` is a projection of `Ran` rather than a field kept beside it, deliberately: two
fields carrying one fact can drift, and a guard on a field that cannot drift is a guard nothing
can fail. It is stated here so it is not mistaken for a guarded claim.

The four answers, and the one that did not exist:

* `OnAMapItNeverReached` — that map is the job.
* `ItRanTheScriptHere` — the only one that licenses a reason.
* `ItRanTheSameBlockOnAnotherMap` — **new.** It was silently the first case, with a merged reason.
* `ItNeverRanTheScript` — it stood on the map and never on the square.

---

## Guards broken on purpose

| break | caught by |
|---|---|
| the map drops out of the key at the write site | four of the five |
| the verdict asks the address alone, as it did | `TheVerdictFollowsTheMapAndNotTheAddress` |
| the fourth answer folds back into never-ran-it | `TheVerdictFollowsTheMapAndNotTheAddress` |
| everything not run here is the shared-block answer | `ABlockNobodyRanAnywhereIsNotABlockRunSomewhereElse` |
| the reason merges across maps as well as passes | `WhyItStoppedDoesNotTravelBetweenTowns` |

**None came back green.** Every fixture has two maps in it, per 194's rule — one map is exactly
what 193's tests had and one map is exactly what could not see this. The fourth break is 195's
lesson applied in advance rather than discovered: the ordinary case, asserted, so that
"everything is the interesting answer" cannot pass.

2771 → 2776 tests, all green.

---

## Task 5, read at last: the refusals and the yes-or-nos

195 made these four counts places rather than times and nobody had looked at what they say.

**The floor's shopping list is four places, and three of them are one person.** `10.5` wants
FRESH WATER, SODA POP and LEMONADE, all sold at exactly one place — `3.13` object 1 — reachable
only by boat. That is the drink thread, already dead in `the-drink-and-the-boat.md`.

With the boat open it is six, and the shape changes completely:

```
  6 places asked for something it was not carrying — this is the shopping list
    10.5  FRESH WATER / SODA POP / LEMONADE   sold at 1 place(s), 1 of them on ground it reached
    14.1  POKé DOLL                            sold at 1 place(s), 1 of them on ground it reached
    33.1  2 x TINYMUSHROOM                     NOTHING ON ANY MAP HANDS ONE OVER — from a routine
    33.1  1 x BIG MUSHROOM                     NOTHING ON ANY MAP HANDS ONE OVER — from a routine
```

**Four of the six are standing on ground where the thing is sold.** That is not a reach problem
and it has never been named as anything. It is a bag problem, and the cause is one line:

```csharp
if (money > 0 || played.Bought.Count > 0)
```

The run starts with nothing, so it buys nothing, so the entire buying report — what it bought,
what it could not afford, what it had left — **prints nothing at all**, and a silent zero reads
exactly like nothing to say. Money is modelled and the payout table has never been located,
which is the known half. The unknown half is that four shopping-list entries are one purchase
away and the output does not say so.

The two `33.1` mushrooms are the other shape: the code boundary with a number on it, the same as
`0x0089` at MT. EMBER.

**The hanging-question list**: 40 places across 29 maps at the floor, and `no reachable script
was left hanging at a yes-or-no` once `--say-yes` is on. The lever answers all of them.

---

## What is still owed

* **The buying report is silent because the run has no money.** Four of the six shopping-list
  entries are on ground where the item is sold. That is the next measurable thing here, and it
  needs the payout table or a modelled purse said out loud as MODELLED.
* `--entries` still reads only the scripts the map scan opens — 0.6% of the file. Unasked.
* The grep 194 asked for is done and found one thing. Everything else the run keys is already
  `(map, ...)`: `moved`, `gone`, `spokenTo`, `handovers`, `walkedFrom`, `refused`, and all five
  of 195's counted sets. `alreadyRun` is a per-map local, which is the same key by scope.
* The tally that prints the four verdicts lives in `Program.cs` and no test reaches it. The
  **rule** it counts is on `Attempt` and is guarded five ways; the counting is presentation.
  Said here rather than left to be discovered.

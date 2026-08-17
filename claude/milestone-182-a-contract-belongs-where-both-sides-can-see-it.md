# Milestone 182 — A contract belongs where both sides can see it

Delivered as `claude-218.bundle` on the tip of `217`. 2696 tests green from a clean clone at
the base. **Not one number on the cartridge moved**, which is the point.

## What this is

The debt at the top of the roadmap, paid. Milestone 181 shipped three fixes and two of them
were in `Program.cs` — a file with no tests, that no fixture can reach, and that nothing can
break on purpose. Both breaks were run and **both came back green**. The same structural fault
had been found five times in six milestones: `--flags`, the wall list, `--scripts`, `--derive`,
and then the playthrough's own reader.

## Why it was stuck there

Running a script is not printing. It is deciding what a scene does given the flags the run
holds, the bag it carries, the trainers it has beaten and the offers it takes — a hundred and
forty lines of it, as a local function inside a reporting method.

It could not move, because it needs `Rom` (in `RomExtract`) and returns `PlayedScript` (in
`Server`), and **neither assembly can see the other**. `Program.cs` was the only place both
were visible. That is not an accident of layout; it is what put the logic there and kept it
there.

So `PlayedScript` moved to `Core`. It is the contract between whatever reads a script and
whatever walks the world, and a contract belongs where both sides can see it. `HowAScriptRuns`
then lives in `RomExtract`, with the rest of the cartridge reading.

## The proof it is the same program

```
--play --say-yes    215 of 425 maps, 195 flags, 31 field moves
                    281 fights won, 52 lost to (103 attempts), 59 never fought
                    party: #131 at 59, #133 at 59, #106 at 59
```

Identical before and after, checked against the pre-refactor build rather than against memory
— which caught a stale number I had written down two runs earlier and would otherwise have
reported as a change.

## The two breaks that were green last milestone

Both bite now, immediately:

* **the reader is never told who has been beaten** → `AFightAlreadyWonIsNotInTheWayAnyMore`
  fails. A `trainerbattle` is its own conditional; a fixture with a fight and a `setflag` behind
  it can now say so.
* **the continuation drops the variables** → `WhatTheFirstHalfOfASceneWroteIsStillThereInTheSecond`
  fails. A fixture with a ball that writes its species, asks, and hands over on the far side.

And both have their opposite half, because a fix that opens everything is the same fault in the
direction nobody notices: a fight *not* won is still in the way, and a run that never answers
still gets nothing.

## And one break that was green because the fixture was wrong

Writing nought into the answer variable — "answer no to everything" — passed. The fixture put
the `givemon` unconditionally after the question, so yes and no reached the same command and
nothing in the test was about answering at all.

Fixed by making the fixture read the answer with a `compare` and a branch, the way the
cartridge does. **And one test written along the way was deleted rather than kept**: it was
named `AnsweringNoHandsNothingOver` and did not answer no. A test whose name does not match
what it asserts is worse than no test, and this session has spent five milestones on exactly
that failure in other people's code.

## What is next

* **CERULEAN CAVE is the only "somebody is standing in the way" left.** `0x005C`, set by `32.0`
  ONE ISLAND person 3 — the ferry thread.
* **Who writes `0x4055`.** The starter is behind that variable; `--var 0x4055=2` is a stand-in.
  `--in-the-image` answers this for flags and there is no equivalent for variables.
* **`0x3F`, `0xE6`, `0xC0`, `0xA7`** — unknown commands ranked by what is behind them.
* **The four that no width reads on from**; **the five wall flags**.
* **The remaining hand-rolled enumerations in `Program.cs`.** The largest one is out; the
  ~28 map walks in the reporting methods are not, and they are the same fault waiting.

## Still open, unchanged

Held items; signs never run; the nine `ARRIVED ON AN ISLAND`s; eleven maps with no way in;
shortest-chain ways in; `Bag.PocketCapacity` in shipped saves; money modelled;
`SpecialContracts.ComparedAfter`; co-op step 4; `StoryClosure` as the no-bag control;
`MapScripts` with no coverage at all; milestone docs for `StoryClosure`, `Autoplayer` and
`SpecialContracts`; sound; and whether `Reachable` should honour a trigger's own condition.
